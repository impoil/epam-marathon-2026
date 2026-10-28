#!/usr/bin/env bash
# Local checks only: no AWS authentication, plan, apply, or cloud API calls.
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

terraform_bin="${TERRAFORM_BIN:-terraform}"
python_bin="${PYTHON_BIN:-python3.14}"
if [[ -z "${TERRAFORM_BIN:-}" && -x .tools/terraform/terraform ]]; then
  terraform_bin="$PWD/.tools/terraform/terraform"
fi
if [[ -z "${PYTHON_BIN:-}" ]] && ! command -v "$python_bin" >/dev/null; then
  for candidate in "$PWD"/.tools/python/cpython-3.14*/bin/python3.14; do
    if [[ -x "$candidate" ]]; then
      python_bin="$candidate"
      break
    fi
  done
fi
"$python_bin" -c 'import sys; assert sys.version_info[:2] == (3, 14), "Python 3.14 is required"; print(sys.version)'

export TF_IN_AUTOMATION=1
export TF_INPUT=0
export CHECKPOINT_DISABLE=1
export TF_DATA_DIR="$PWD/.terraform"
"$terraform_bin" fmt -check
"$terraform_bin" init -backend=false -input=false -lockfile=readonly
"$terraform_bin" validate
"$python_bin" -m unittest -v
