output "backend_url" { value = "https://${aws_api_gateway_rest_api.app.id}.execute-api.eu-north-1.amazonaws.com/${aws_api_gateway_stage.app.stage_name}" }
output "frontend_url" {
  description = "Configured external production origin; does not prove a Pages project exists or has been published."
  value       = var.frontend_origin
}
output "frontend_bucket" {
  description = "Retained unused private S3 bucket pending separately approved cleanup. Do not upload the frontend here."
  value       = aws_s3_bucket.frontend.id
}
output "user_pool_id" { value = aws_cognito_user_pool.app.id }
output "user_pool_client_id" { value = aws_cognito_user_pool_client.app.id }
output "tables" { value = { for key, table in aws_dynamodb_table.app : key => table.name } }
