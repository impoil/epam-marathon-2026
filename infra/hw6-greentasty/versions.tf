terraform {
  required_version = ">= 1.10, < 2.0"
  backend "s3" {}
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.70.0"
    }
  }
}

provider "aws" {
  region              = "eu-north-1"
  profile             = "fresh-deploy"
  allowed_account_ids = ["071954286972"]
  default_tags {
    tags = { Project = "hw6-greentasty", Purpose = "epam-homework5-6-demo", ManagedBy = "Terraform" }
  }
}
