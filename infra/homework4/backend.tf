terraform {
  # Deliberately partial: real deployments must initialize the private S3 backend.
  # Offline validation uses init -backend=false; it is not a deployment setup.
  backend "s3" {}
}
