pipeline {
    agent any

    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timeout(time: 20, unit: 'MINUTES')
        timestamps()
    }

    environment {
        COMPOSE_PROJECT_NAME = 'llm-integration-demo'
        REPO_DIR             = '/lump/apps/llm-integration-demo'
        API_URL              = 'http://127.0.0.1:5252'
    }

    stages {

        stage('Checkout') {
            steps {
                // Local bare-metal checkout — no SCM poll, triggered by post-commit hook
                checkout([
                    $class: 'GitSCM',
                    branches: [[name: '*/main']],
                    userRemoteConfigs: [[url: "file://${env.REPO_DIR}"]],
                    extensions: [[$class: 'LocalBranch', localBranch: 'main']]
                ])
            }
        }

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

        stage('Deploy') {
            steps {
                dir("${env.REPO_DIR}") {
                    sh 'docker compose up -d --remove-orphans'
                }
            }
        }

        stage('Proof of Life') {
            steps {
                script {
                    // Give containers a moment to finish starting
                    sleep(time: 8, unit: 'SECONDS')

                    // 1 — API health: POST a simple query, expect HTTP 200 and status=success
                    def apiResult = sh(
                        script: """
                            curl -sf -X POST ${env.API_URL}/analytics/query \\
                                -H 'Content-Type: application/json' \\
                                -d '{"question":"Total revenue last month"}' \\
                                -o /tmp/grounded-pol.json \\
                                -w '%{http_code}'
                        """,
                        returnStdout: true
                    ).trim()

                    if (apiResult != '200') {
                        error("API proof-of-life failed: HTTP ${apiResult}")
                    }

                    def status = sh(
                        script: "python3 -c \"import json; d=json.load(open('/tmp/grounded-pol.json')); print(d.get('status',''))\"",
                        returnStdout: true
                    ).trim()

                    if (status != 'success') {
                        def body = readFile('/tmp/grounded-pol.json')
                        error("API proof-of-life returned status '${status}':\n${body}")
                    }

                    echo "API proof-of-life passed (status=${status})"

                    // 2 — UI health: nginx should serve the SPA shell with HTTP 200
                    def uiCode = sh(
                        script: "curl -sf -o /dev/null -w '%{http_code}' http://127.0.0.1:5173/grounded/",
                        returnStdout: true
                    ).trim()

                    if (uiCode != '200') {
                        error("UI proof-of-life failed: HTTP ${uiCode}")
                    }

                    echo "UI proof-of-life passed (HTTP ${uiCode})"
                }
            }
        }

        stage('Publish Artifacts') {
            steps {
                script {
                    def commit   = sh(script: 'git rev-parse --short HEAD', returnStdout: true).trim()
                    def ts       = sh(script: 'date +%Y%m%d-%H%M%S', returnStdout: true).trim()
                    def tag      = "${ts}-${commit}"
                    def apiImage = "llm-integration-demo-api:${tag}"
                    def uiImage  = "llm-integration-demo-ui:${tag}"

                    sh "docker tag llm-integration-demo-api:latest ${apiImage}"
                    sh "docker tag llm-integration-demo-ui:latest ${uiImage}"

                    // Save tarballs to the repo's artifacts dir so they survive workspace cleanup
                    sh "mkdir -p ${env.REPO_DIR}/ci-artifacts"
                    sh "docker save ${apiImage} | gzip > ${env.REPO_DIR}/ci-artifacts/api-${tag}.tar.gz"
                    sh "docker save ${uiImage}  | gzip > ${env.REPO_DIR}/ci-artifacts/ui-${tag}.tar.gz"

                    // Keep only the 5 most recent tarballs per image
                    sh "ls -t ${env.REPO_DIR}/ci-artifacts/api-*.tar.gz 2>/dev/null | tail -n +6 | xargs -r rm --"
                    sh "ls -t ${env.REPO_DIR}/ci-artifacts/ui-*.tar.gz  2>/dev/null | tail -n +6 | xargs -r rm --"

                    echo "Artifacts saved: api-${tag}.tar.gz  ui-${tag}.tar.gz"

                    // Surface the tag so it appears in the Jenkins build summary
                    currentBuild.description = "tag: ${tag}"
                }

                // Archive the tarballs in Jenkins so they show up in the build UI
                archiveArtifacts(
                    artifacts: "ci-artifacts/*.tar.gz",
                    allowEmptyArchive: false,
                    fingerprint: true
                )
            }
        }
    }

    post {
        success {
            echo "Build, deploy, proof-of-life, and artifact publish all succeeded."
        }
        failure {
            echo "Pipeline failed — check stage output above."
            // Emit running container state to help diagnose deploy failures
            sh 'docker compose -f ${REPO_DIR}/compose.yaml ps || true'
            sh 'docker compose -f ${REPO_DIR}/compose.yaml logs --tail=50 api || true'
        }
        cleanup {
            sh 'rm -f /tmp/grounded-pol.json'
        }
    }
}
