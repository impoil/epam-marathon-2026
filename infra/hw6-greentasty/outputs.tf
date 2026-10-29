output "backend_url" { value = "https://${aws_api_gateway_rest_api.app.id}.execute-api.eu-north-1.amazonaws.com/${aws_api_gateway_stage.app.stage_name}" }
output "frontend_url" { value = "https://${aws_cloudfront_distribution.frontend.domain_name}" }
output "frontend_bucket" { value = aws_s3_bucket.frontend.id }
output "distribution_id" { value = aws_cloudfront_distribution.frontend.id }
output "user_pool_id" { value = aws_cognito_user_pool.app.id }
output "user_pool_client_id" { value = aws_cognito_user_pool_client.app.id }
output "tables" { value = { for key, table in aws_dynamodb_table.app : key => table.name } }
