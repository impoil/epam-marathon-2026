variable "name" {
  description = "Unique resource name within this AWS account and region."
  type        = string
  default     = "epam-hw4-group-preorder"

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{2,49}$", var.name))
    error_message = "Use 3–50 lowercase letters, digits or hyphens, starting with a letter."
  }
}
