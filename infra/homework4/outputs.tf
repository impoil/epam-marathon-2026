output "group_preorder_url" {
  description = "Public read-only GET endpoint; available only after an authorized deployment."
  value       = "${aws_apigatewayv2_api.summary.api_endpoint}/group-preorder"
}

output "lambda_function_name" {
  value = aws_lambda_function.summary.function_name
}

output "log_groups" {
  value = {
    lambda = aws_cloudwatch_log_group.lambda.name
    api    = aws_cloudwatch_log_group.api.name
  }
}
