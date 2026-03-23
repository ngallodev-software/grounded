.PHONY: check-https check-https-public

check-https:
	bash scripts/check-cloudflare-https.sh

check-https-public:
	CHECK_LOCAL=0 START_STACK=0 bash scripts/check-cloudflare-https.sh
