resource "aws_cognito_user_pool" "app" {
  name                = local.prefix
  username_attributes = ["email"]
  user_pool_tier      = "LITE"
  deletion_protection = "ACTIVE"
  mfa_configuration   = "OFF"
  admin_create_user_config { allow_admin_create_user_only = true }
  username_configuration { case_sensitive = false }
  password_policy {
    minimum_length                   = 14
    require_lowercase                = true
    require_uppercase                = true
    require_numbers                  = true
    require_symbols                  = true
    temporary_password_validity_days = 1
  }
  account_recovery_setting {
    recovery_mechanism {
      name     = "admin_only"
      priority = 1
    }
  }
  lifecycle { prevent_destroy = true }
}

resource "aws_cognito_user_pool_client" "app" {
  name                          = local.prefix
  user_pool_id                  = aws_cognito_user_pool.app.id
  generate_secret               = false
  explicit_auth_flows           = ["ALLOW_USER_PASSWORD_AUTH", "ALLOW_REFRESH_TOKEN_AUTH"]
  prevent_user_existence_errors = "ENABLED"
  enable_token_revocation       = true
  access_token_validity         = 30
  id_token_validity             = 30
  refresh_token_validity        = 1
  token_validity_units {
    access_token  = "minutes"
    id_token      = "minutes"
    refresh_token = "days"
  }
}

resource "aws_cognito_user_group" "customer" {
  name         = "Customer"
  user_pool_id = aws_cognito_user_pool.app.id
  description  = "Reservation owner is a Customer who owns that reservation; Owner is not a Cognito role."
}
