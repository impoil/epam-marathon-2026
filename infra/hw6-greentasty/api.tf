locals {
  routes = jsondecode(file("${path.module}/routes.json"))
  origin = var.frontend_origin
  cors_headers = {
    "Access-Control-Allow-Origin"  = { type = "string" }
    "Access-Control-Allow-Methods" = { type = "string" }
    "Access-Control-Allow-Headers" = { type = "string" }
  }
  gateway_responses = {
    for response_type in ["DEFAULT_4XX", "DEFAULT_5XX"] : response_type => {
      response_parameters = {
        "gatewayresponse.header.Access-Control-Allow-Origin"  = "'${local.origin}'"
        "gatewayresponse.header.Access-Control-Allow-Headers" = "'Content-Type,Authorization'"
        "gatewayresponse.header.Access-Control-Allow-Methods" = "'GET,POST,PUT,DELETE,OPTIONS'"
        "gatewayresponse.header.Cache-Control"                = "'no-store'"
      }
      # API Gateway returns this default on read, even if omitted on creation.
      response_templates = {
        "application/json" = "{\"message\":$context.error.messageString}"
      }
    }
  }
  api_definition = {
    swagger = "2.0"
    info    = { title = local.prefix, version = "1.0" }
    schemes = ["https"]
    securityDefinitions = {
      cognito = {
        type                           = "apiKey", name = "Authorization", in = "header"
        "x-amazon-apigateway-authtype" = "cognito_user_pools"
        "x-amazon-apigateway-authorizer" = {
          type                         = "cognito_user_pools"
          providerARNs                 = [aws_cognito_user_pool.app.arn]
          identityValidationExpression = "^${aws_cognito_user_pool_client.app.id}$"
        }
      }
    }
    paths = {
      for path, methods in local.routes : path => merge({
        for method, auth in methods : lower(method) => {
          security  = auth == "cognito" ? [{ cognito = [] }] : []
          responses = { "200" = { description = "Application response" } }
          "x-amazon-apigateway-integration" = {
            type = "aws_proxy", httpMethod = "POST", uri = aws_lambda_function.api.invoke_arn
          }
        }
        }, {
        options = {
          security  = []
          responses = { "200" = { description = "CORS", headers = local.cors_headers } }
          "x-amazon-apigateway-integration" = {
            type = "mock", requestTemplates = { "application/json" = "{\"statusCode\":200}" }
            responses = { default = {
              statusCode = "200"
              responseParameters = {
                "method.response.header.Access-Control-Allow-Origin"  = "'${local.origin}'"
                "method.response.header.Access-Control-Allow-Methods" = "'GET,POST,PUT,DELETE,OPTIONS'"
                "method.response.header.Access-Control-Allow-Headers" = "'Content-Type,Authorization'"
              }
            } }
          }
        }
      })
    }
  }
}

resource "aws_api_gateway_rest_api" "app" {
  name              = local.prefix
  body              = jsonencode(local.api_definition)
  put_rest_api_mode = "overwrite"
  endpoint_configuration { types = ["REGIONAL"] }
}

resource "aws_lambda_permission" "gateway" {
  statement_id   = "OnlyHw6RestApi"
  action         = "lambda:InvokeFunction"
  function_name  = aws_lambda_function.api.function_name
  principal      = "apigateway.amazonaws.com"
  source_arn     = "${aws_api_gateway_rest_api.app.execution_arn}/demo/*/*"
  source_account = "071954286972"
}

resource "aws_api_gateway_deployment" "app" {
  rest_api_id = aws_api_gateway_rest_api.app.id
  triggers = {
    definition = sha256(jsonencode(local.api_definition))
    responses  = sha256(jsonencode(local.gateway_responses))
  }
  # The trigger hashes desired values only; retain response update ordering.
  depends_on = [aws_api_gateway_gateway_response.cors]
  lifecycle { create_before_destroy = true }
}

resource "aws_api_gateway_stage" "app" {
  rest_api_id   = aws_api_gateway_rest_api.app.id
  deployment_id = aws_api_gateway_deployment.app.id
  stage_name    = "demo"
  # REST execution/access logging requires an account-wide CloudWatch role setting.
  # Do not mutate that setting shared with HW4. Lambda request IDs provide diagnostics.
}

resource "aws_api_gateway_method_settings" "limits" {
  rest_api_id = aws_api_gateway_rest_api.app.id
  stage_name  = aws_api_gateway_stage.app.stage_name
  method_path = "*/*"
  settings {
    throttling_rate_limit  = 5
    throttling_burst_limit = 10
    logging_level          = "OFF"
    data_trace_enabled     = false
    metrics_enabled        = false
  }
}

resource "aws_api_gateway_gateway_response" "cors" {
  for_each            = local.gateway_responses
  rest_api_id         = aws_api_gateway_rest_api.app.id
  response_type       = each.key
  response_parameters = each.value.response_parameters
  response_templates  = each.value.response_templates
}
