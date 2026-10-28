"""Read-only educational group pre-order API; no AWS SDK or external dependencies."""

import json


CATALOG = {
    "strawberry-mint-salad": {
        "name": "Fresh Strawberry Mint Salad",
        "unit_price_cents": 1700,
    },
    "sweet-potato-lentils-salad": {
        "name": "Roasted Sweet Potato & Lentils Salad",
        "unit_price_cents": 1000,
    },
}

PARTICIPANTS = (
    {
        "id": "owner",
        "name": "Owner",
        "role": "owner",
        "orders": (
            {"dish_id": "strawberry-mint-salad", "quantity": 1},
            {"dish_id": "sweet-potato-lentils-salad", "quantity": 1},
        ),
    },
    {
        "id": "anna",
        "name": "Anna",
        "role": "guest",
        "orders": ({"dish_id": "strawberry-mint-salad", "quantity": 1},),
    },
    {
        "id": "oleh",
        "name": "Oleh",
        "role": "guest",
        "orders": ({"dish_id": "sweet-potato-lentils-salad", "quantity": 2},),
    },
    {"id": "maria", "name": "Maria", "role": "guest", "orders": ()},
)


def usd(cents):
    """Convert to a JSON USD amount only after doing all arithmetic in integer cents."""
    return cents / 100


def build_summary(participants=PARTICIPANTS, catalog=CATALOG):
    """Build a fresh response without modifying the sample data or persisting anything."""
    result = []
    group_total_cents = 0
    total_quantity = 0
    participants_with_orders = 0

    for participant in participants:
        orders = []
        subtotal_cents = 0
        participant_quantity = 0
        for order in participant["orders"]:
            quantity = order["quantity"]
            if type(quantity) is not int or quantity <= 0:
                raise ValueError("Quantity must be a positive integer")
            dish_id = order["dish_id"]
            if dish_id not in catalog:
                raise ValueError("Unknown dish")
            dish = catalog[dish_id]
            price = dish["unit_price_cents"]
            if type(price) is not int or price < 0:
                raise ValueError("Price must be a non-negative integer number of cents")
            line_total_cents = price * quantity
            orders.append({
                "dish_id": dish_id,
                "dish_name": dish["name"],
                "unit_price": usd(price),
                "quantity": quantity,
                "line_total": usd(line_total_cents),
            })
            subtotal_cents += line_total_cents
            participant_quantity += quantity

        has_orders = bool(orders)
        result.append({
            "participant_id": participant["id"],
            "name": participant["name"],
            "role": participant["role"],
            "status": "selected" if has_orders else "pending",
            "orders": orders,
            "total_quantity": participant_quantity,
            "subtotal": usd(subtotal_cents),
        })
        group_total_cents += subtotal_cents
        total_quantity += participant_quantity
        participants_with_orders += int(has_orders)

    return {
        "schema_version": "1.0",
        "currency": "USD",
        "participants": result,
        "participant_count": len(result),
        "participants_with_orders": participants_with_orders,
        "participants_pending": len(result) - participants_with_orders,
        "total_quantity": total_quantity,
        "group_total": usd(group_total_cents),
    }


def response(status_code, body, extra_headers=None):
    return {
        "statusCode": status_code,
        "headers": {
            "Content-Type": "application/json; charset=utf-8",
            "Cache-Control": "no-store",
            **(extra_headers or {}),
        },
        "isBase64Encoded": False,
        "body": json.dumps(body, ensure_ascii=False, allow_nan=False),
    }


def handler(event, context):
    """Handle API Gateway HTTP API payload format 2.0, without accepting mutations."""
    method = event.get("requestContext", {}).get("http", {}).get("method")
    if method != "GET":
        return response(405, {"error": {"code": "method_not_allowed"}}, {"Allow": "GET"})
    if event.get("rawPath") != "/group-preorder":
        return response(404, {"error": {"code": "not_found"}})
    return response(200, build_summary())
