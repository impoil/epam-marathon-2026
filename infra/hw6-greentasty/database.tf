locals {
  prefix = "hw6-greentasty"
  # Exact keys and index names from the application; all physical table names are isolated.
  tables = jsondecode(file("${path.module}/tables.json"))
}

resource "aws_dynamodb_table" "app" {
  for_each     = local.tables
  name         = "${local.prefix}-${each.key}"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = each.value.hash
  range_key    = try(each.value.range, null)

  dynamic "attribute" {
    for_each = each.value.attributes
    content {
      name = attribute.key
      type = attribute.value
    }
  }
  dynamic "global_secondary_index" {
    for_each = each.value.indexes
    content {
      name            = global_secondary_index.key
      hash_key        = global_secondary_index.value.hash
      range_key       = try(global_secondary_index.value.range, null)
      projection_type = "ALL"
      on_demand_throughput {
        max_read_request_units  = 10
        max_write_request_units = 5
      }
    }
  }
  on_demand_throughput {
    max_read_request_units  = 10
    max_write_request_units = 5
  }
  lifecycle { prevent_destroy = true }
}
