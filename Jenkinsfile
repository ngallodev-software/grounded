pipeline {
    agent any

    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timeout(time: 20, unit: 'MINUTES')
        timestamps()
    }

    environment {
        REPO_DIR      = '/lump/apps/llm-integration-demo'
        API_URL       = 'http://127.0.0.1:5252'
        EXTERNAL_URL  = 'https://ngallodev-software.uk/grounded'
        NTFY_TOPIC    = 'grounded-ci-478831ef8344'
    }

    stages {

        stage('Checkout') {
            steps {
                checkout([
                    $class: 'GitSCM',
                    branches: [[name: '*/main']],
                    userRemoteConfigs: [[url: "file://${env.REPO_DIR}"]],
                    extensions: [[$class: 'LocalBranch', localBranch: 'main']]
                ])
            }
        }

        stage('Test') {
            steps {
                dir("${env.REPO_DIR}") {
                    // Docker builds leave obj/ and bin/ owned by the container user (uid 1000).
                    // Reclaim ownership so dotnet can write its temp files, then build + test.
                    sh 'find . -type d \\( -name obj -o -name bin \\) -exec chown -R jenkins:jenkins {} + 2>/dev/null || true'
                    sh 'dotnet test Grounded.Tests/Grounded.Tests.csproj --configuration Release --logger "console;verbosity=normal"'
                }
            }
        }

        stage('Build') {
            parallel {
                stage('Build API') {
                    steps {
                        dir("${env.REPO_DIR}") {
                            sh 'docker compose build api'
                        }
                    }
                }
                stage('Build UI') {
                    steps {
                        dir("${env.REPO_DIR}") {
                            sh 'docker compose build ui'
                        }
                    }
                }
            }
        }

        stage('Publish Artifacts') {
            steps {
                script {
                    def commit = sh(script: 'git rev-parse --short HEAD', returnStdout: true).trim()
                    def ts     = sh(script: 'date +%Y%m%d-%H%M%S', returnStdout: true).trim()
                    def tag    = "${ts}-${commit}"

                    sh "docker tag llm-integration-demo-api:latest llm-integration-demo-api:${tag}"
                    sh "docker tag llm-integration-demo-ui:latest  llm-integration-demo-ui:${tag}"

                    sh "mkdir -p ${env.REPO_DIR}/ci-artifacts"
                    // Save to both the persistent repo dir and the workspace so archiveArtifacts can find them
                    sh "docker save llm-integration-demo-api:${tag} | gzip > ${env.REPO_DIR}/ci-artifacts/api-${tag}.tar.gz"
                    sh "docker save llm-integration-demo-ui:${tag}  | gzip > ${env.REPO_DIR}/ci-artifacts/ui-${tag}.tar.gz"

                    // Keep last 5 of each in the persistent dir
                    sh "ls -t ${env.REPO_DIR}/ci-artifacts/api-*.tar.gz 2>/dev/null | tail -n +6 | xargs -r rm --"
                    sh "ls -t ${env.REPO_DIR}/ci-artifacts/ui-*.tar.gz  2>/dev/null | tail -n +6 | xargs -r rm --"

                    // Symlink into workspace so archiveArtifacts can resolve them
                    sh "mkdir -p ci-artifacts"
                    sh "ln -sf ${env.REPO_DIR}/ci-artifacts/api-${tag}.tar.gz ci-artifacts/api-${tag}.tar.gz"
                    sh "ln -sf ${env.REPO_DIR}/ci-artifacts/ui-${tag}.tar.gz  ci-artifacts/ui-${tag}.tar.gz"

                    currentBuild.description = "tag: ${tag}"
                    echo "Artifacts published: api-${tag}.tar.gz  ui-${tag}.tar.gz"
                }
                archiveArtifacts(
                    artifacts: 'ci-artifacts/*.tar.gz',
                    allowEmptyArchive: false,
                    fingerprint: false
                )
            }
        }

        stage('Deploy to Production') {
            steps {
                dir("${env.REPO_DIR}") {
                    script {
                        // Only recreate api and ui — never touch postgres or cloudflared.
                        // --no-deps prevents compose from cascading restarts to dependents.
                        // Check if each container is already running; if so, do an in-place
                        // recreate of just that service so the live site has zero downtime gaps
                        // from unrelated containers being torn down.
                        def apiRunning  = sh(script: 'docker inspect -f "{{.State.Running}}" grounded-api  2>/dev/null || echo false', returnStdout: true).trim()
                        def uiRunning   = sh(script: 'docker inspect -f "{{.State.Running}}" grounded-ui   2>/dev/null || echo false', returnStdout: true).trim()

                        if (apiRunning == 'true') {
                            sh 'docker compose up -d --no-deps --force-recreate api'
                        } else {
                            sh 'docker compose up -d --no-deps api'
                        }

                        if (uiRunning == 'true') {
                            sh 'docker compose up -d --no-deps --force-recreate ui'
                        } else {
                            sh 'docker compose up -d --no-deps ui'
                        }
                    }
                }
            }
        }

        stage('Proof of Life') {
            steps {
                script {
                    sleep(time: 10, unit: 'SECONDS')

                    // --- Internal API ---
                    def apiCode = sh(
                        script: """
                            curl -sf -X POST ${env.API_URL}/analytics/query \
                                -H 'Content-Type: application/json' \
                                -d '{"question":"Total revenue last month"}' \
                                -o /tmp/grounded-pol.json \
                                -w '%{http_code}'
                        """,
                        returnStdout: true
                    ).trim()

                    if (apiCode != '200') {
                        error("Internal API proof-of-life failed: HTTP ${apiCode}")
                    }

                    def apiStatus = sh(
                        script: "python3 -c \"import json; d=json.load(open('/tmp/grounded-pol.json')); print(d.get('status',''))\"",
                        returnStdout: true
                    ).trim()

                    if (apiStatus != 'success') {
                        error("Internal API returned status='${apiStatus}' — check query pipeline")
                    }
                    echo "Internal API: OK (status=${apiStatus})"

                    // --- Internal UI --- verify the container TLS endpoint, not just HTTP 200
                    def uiBody = sh(
                        script: "docker compose -f ${env.REPO_DIR}/compose.yaml exec -T ui wget --no-check-certificate -qO- https://127.0.0.1/",
                        returnStdout: true
                    ).trim()

                    if (!uiBody.contains('<div id="root">')) {
                        error("Internal UI proof-of-life failed: response did not contain app root element")
                    }
                    echo "Internal UI: OK (app root element present)"

                    // --- Cloudflare tunnel connectivity ---
                    // The external URL sits behind Cloudflare Access so curl can't authenticate.
                    // Instead verify the tunnel daemon has active registered connections.
                    def tunnelConnections = sh(
                        script: "docker compose -f ${env.REPO_DIR}/compose.yaml logs --tail=500 cloudflared 2>/dev/null | grep -c 'Registered tunnel connection' || true",
                        returnStdout: true
                    ).trim().toInteger()

                    if (tunnelConnections == 0) {
                        error("Cloudflare tunnel proof-of-life failed: no registered connections found in cloudflared logs")
                    }
                    echo "Cloudflare tunnel: OK (${tunnelConnections} registered connection(s))"
                }
            }
        }
    }

    post {
        success {
            script {
                def commit = sh(script: 'git log -1 --pretty=format:"%h %s"', returnStdout: true).trim()
                ntfyNotify(
                    env.NTFY_TOPIC,
                    'Grounded CI - Build Passed',
                    "Build #${env.BUILD_NUMBER} deployed successfully.\n${commit}\nInternal + external proof-of-life passed.",
                    'rocket',
                    'default'
                )
            }
        }
        failure {
            script {
                def commit = sh(script: 'git log -1 --pretty=format:"%h %s"', returnStdout: true).trim()
                ntfyNotify(
                    env.NTFY_TOPIC,
                    'Grounded CI - Build FAILED',
                    "Build #${env.BUILD_NUMBER} failed at stage: ${env.STAGE_NAME ?: 'unknown'}.\n${commit}\nConsole: ${env.BUILD_URL}console",
                    'rotating_light',
                    'high'
                )
                // Log production container state to help diagnose
                sh "docker compose -f ${env.REPO_DIR}/compose.yaml ps || true"
                sh "docker compose -f ${env.REPO_DIR}/compose.yaml logs --tail=40 api || true"
            }
        }
        cleanup {
            sh 'rm -f /tmp/grounded-pol.json'
        }
    }
}

// Send a notification to ntfy.sh — uses env vars to avoid shell quoting issues
def ntfyNotify(String topic, String title, String message, String tags, String priority) {
    withEnv([
        "NTFY_TITLE=${title}",
        "NTFY_MESSAGE=${message}",
        "NTFY_TAGS=${tags}",
        "NTFY_PRIORITY=${priority}",
        "NTFY_TOPIC=${topic}"
    ]) {
        sh '''
            curl -sf \
                -H "Title: ${NTFY_TITLE}" \
                -H "Priority: ${NTFY_PRIORITY}" \
                -H "Tags: ${NTFY_TAGS}" \
                --data-raw "${NTFY_MESSAGE}" \
                "https://ntfy.sh/${NTFY_TOPIC}" || true
        '''
    }
}
