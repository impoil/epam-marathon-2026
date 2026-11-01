resource "aws_cloudwatch_log_group" "lambda" {
  name              = "/aws/lambda/${local.prefix}-api"
  retention_in_days = 7
}

resource "aws_iam_role" "api" {
  name = "${local.prefix}-api"
  assume_role_policy = jsonencode({
    Version   = "2012-10-17"
    Statement = [{ Effect = "Allow", Action = "sts:AssumeRole", Principal = { Service = "lambda.amazonaws.com" } }]
  })
}

resource "aws_iam_role_policy" "api" {
  role = aws_iam_role.api.id
  name = "${local.prefix}-runtime"
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      { Effect = "Allow", Action = ["logs:CreateLogStream", "logs:PutLogEvents"], Resource = "${aws_cloudwatch_log_group.lambda.arn}:*" },
      { Effect = "Allow", Action = ["dynamodb:GetItem", "dynamodb:BatchGetItem", "dynamodb:Scan", "dynamodb:Query", "dynamodb:DescribeTable"],
      Resource = flatten([for table in aws_dynamodb_table.app : [table.arn, "${table.arn}/index/*"]]) },
      # DynamoDB authorizes each item in TransactWriteItems using PutItem/ConditionCheckItem.
      { Effect = "Allow", Action = ["dynamodb:PutItem", "dynamodb:UpdateItem", "dynamodb:DeleteItem", "dynamodb:ConditionCheckItem"],
      Resource = [for name in ["Reservations", "GroupReservations", "GuestSessions", "Orders", "Waiters", "Notifications"] : aws_dynamodb_table.app[name].arn] },
      { Effect = "Allow", Action = ["cognito-idp:AdminGetUser", "cognito-idp:ListUsers", "cognito-idp:AdminListGroupsForUser"],
      Resource = aws_cognito_user_pool.app.arn }
    ]
  })
}

resource "aws_lambda_function" "api" {
  function_name                  = "${local.prefix}-api"
  role                           = aws_iam_role.api.arn
  runtime                        = "dotnet10"
  architectures                  = ["x86_64"]
  handler                        = "APIHandler::APIHandler.Function::FunctionHandler"
  filename                       = var.lambda_zip
  source_code_hash               = filebase64sha256(var.lambda_zip)
  memory_size                    = 512
  timeout                        = 15
  reserved_concurrent_executions = var.reserved_concurrency
  environment {
    variables = {
      REGION                            = "eu-north-1"
      COGNITO_USER_POOL_ID              = aws_cognito_user_pool.app.id
      COGNITO_USER_POOL_CLIENT_ID       = aws_cognito_user_pool_client.app.id
      CORS_ALLOWED_ORIGIN               = var.frontend_origin
      reservationsTable                 = aws_dynamodb_table.app["Reservations"].name
      groupReservationsTable            = aws_dynamodb_table.app["GroupReservations"].name
      guestSessionTable                 = aws_dynamodb_table.app["GuestSessions"].name
      ordersTable                       = aws_dynamodb_table.app["Orders"].name
      dishesTable                       = aws_dynamodb_table.app["Dishes"].name
      tablesTable                       = aws_dynamodb_table.app["Tables"].name
      locationsTable                    = aws_dynamodb_table.app["Locations"].name
      WaiterListTableName               = aws_dynamodb_table.app["Waiters"].name
      notificationsTable                = aws_dynamodb_table.app["Notifications"].name
      FEEDBACK_TABLE_NAME               = aws_dynamodb_table.app["Feedback"].name
      notificationsUserIdIndex          = "UserIdIndex"
      reservationsCustomerIdGlobalIndex = "GSI_UserId"
      reservationsWaiterIdGlobalIndex   = "GSI_WaiterId"
      reservationsTableGlobalIndex      = "GSI_ByTableId"
      waitersGlobalIndex                = "locationId-index"
      tableLocationIdIndex              = "locationId-tableNumber-index"
      feedbackGlobalIndex               = "locationId-createdAt-index"
      OrderIndexReservationId           = "GSI_ReservationId"
      # Unrouted legacy services are eagerly constructed. Names exist only as config,
      # no resources or IAM grants are created for reports, uploads, or SQS.
      reportsTable            = "${local.prefix}-disabled-reports"
      reportsHistoryTable     = "${local.prefix}-disabled-report-history"
      profileImagesTable      = "${local.prefix}-disabled-profile-images"
      imagesBucketName        = "${local.prefix}-disabled-images"
      profileImagesBucketName = "${local.prefix}-disabled-profile-images"
      reports_queue_name      = "${local.prefix}-disabled-reports"
    }
  }
  depends_on = [aws_iam_role_policy.api]
}
