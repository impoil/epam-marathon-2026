data "archive_file" "lambda" {
  type        = "zip"
  source_file = "${path.module}/lambda_function.py"
  output_path = "${path.module}/group-preorder.zip"
}

resource "aws_iam_role" "lambda" {
  name = "${var.name}-execution"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "lambda.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

resource "aws_cloudwatch_log_group" "lambda" {
  name              = "/aws/lambda/${var.name}"
  retention_in_days = 7
}

resource "aws_iam_role_policy" "lambda_logs" {
  name = "write-own-logs"
  role = aws_iam_role.lambda.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["logs:CreateLogStream", "logs:PutLogEvents"]
      Resource = "${aws_cloudwatch_log_group.lambda.arn}:*"
    }]
  })
}

resource "aws_lambda_function" "summary" {
  function_name    = var.name
  description      = "Read-only educational group pre-order summary"
  role             = aws_iam_role.lambda.arn
  runtime          = "python3.14"
  handler          = "lambda_function.handler"
  architectures    = ["arm64"]
  memory_size      = 128
  timeout          = 3
  filename         = data.archive_file.lambda.output_path
  source_code_hash = data.archive_file.lambda.output_base64sha256
  publish          = false

  depends_on = [aws_iam_role_policy.lambda_logs]
}

resource "aws_apigatewayv2_api" "summary" {
  name          = var.name
  protocol_type = "HTTP"
}

resource "aws_apigatewayv2_integration" "summary" {
  api_id                 = aws_apigatewayv2_api.summary.id
  integration_type       = "AWS_PROXY"
  integration_uri        = aws_lambda_function.summary.invoke_arn
  integration_method     = "POST"
  payload_format_version = "2.0"
  timeout_milliseconds   = 5000
}

resource "aws_apigatewayv2_route" "summary" {
  api_id             = aws_apigatewayv2_api.summary.id
  route_key          = "GET /group-preorder"
  authorization_type = "NONE"
  target             = "integrations/${aws_apigatewayv2_integration.summary.id}"
}

resource "aws_cloudwatch_log_group" "api" {
  name              = "/aws/apigateway/${var.name}"
  retention_in_days = 7
}

resource "aws_apigatewayv2_stage" "default" {
  api_id      = aws_apigatewayv2_api.summary.id
  name        = "$default"
  auto_deploy = true

  default_route_settings {
    throttling_rate_limit  = 1
    throttling_burst_limit = 2
  }

  access_log_settings {
    destination_arn = aws_cloudwatch_log_group.api.arn
    format = jsonencode({
      requestId               = "$context.requestId"
      routeKey                = "$context.routeKey"
      status                  = "$context.status"
      responseLength          = "$context.responseLength"
      integrationErrorMessage = "$context.integrationErrorMessage"
    })
  }
}

resource "aws_lambda_permission" "api" {
  statement_id  = "AllowGetGroupPreorder"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.summary.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.summary.execution_arn}/*/GET/group-preorder"
}
