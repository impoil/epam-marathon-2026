variable "lambda_zip" {
  description = "Path to the real .NET 10 linux-x64 publish ZIP, built by scripts/package.ps1."
  type        = string
  default     = "build/api-handler.zip"
  validation {
    condition     = fileexists(var.lambda_zip)
    error_message = "Build the real .NET package first; placeholder ZIPs are not accepted."
  }
}

variable "reserved_concurrency" {
  description = "Null uses account concurrency if AWS refuses a reservation (100 unreserved required). Review quota before apply."
  type        = number
  default     = 2
  nullable    = true
  validation {
    condition     = var.reserved_concurrency == null ? true : contains([1, 2, 3, 4, 5], var.reserved_concurrency)
    error_message = "Use 1–5 concurrent executions, or null after reviewing the account quota."
  }
}
