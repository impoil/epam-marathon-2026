"""Offline tests. Running this module never accesses AWS or the network."""

import copy
import json
import unittest

from lambda_function import CATALOG, PARTICIPANTS, build_summary, handler


def event(method="GET", path="/group-preorder"):
    return {
        "version": "2.0",
        "rawPath": path,
        "requestContext": {"http": {"method": method, "path": path}},
    }


class SummaryTests(unittest.TestCase):
    def test_sample_totals_and_quantities(self):
        summary = build_summary()
        self.assertEqual(summary["currency"], "USD")
        self.assertEqual(summary["group_total"], 64)
        self.assertEqual(summary["total_quantity"], 5)
        self.assertEqual(summary["participant_count"], 4)
        self.assertEqual(summary["participants_with_orders"], 3)
        self.assertEqual(summary["participants_pending"], 1)
        self.assertEqual(
            [(p["name"], p["subtotal"], p["total_quantity"], p["status"])
             for p in summary["participants"]],
            [("Owner", 27, 2, "selected"), ("Anna", 17, 1, "selected"),
             ("Oleh", 20, 2, "selected"), ("Maria", 0, 0, "pending")],
        )

    def test_order_lines(self):
        owner, anna, oleh, maria = build_summary()["participants"]
        self.assertEqual([o["unit_price"] for o in owner["orders"]], [17, 10])
        self.assertEqual(anna["orders"][0]["quantity"], 1)
        self.assertEqual(oleh["orders"][0]["quantity"], 2)
        self.assertEqual(oleh["orders"][0]["line_total"], 20)
        self.assertEqual(maria["orders"], [])
        for participant in (owner, anna, oleh, maria):
            self.assertEqual(participant["subtotal"], sum(o["line_total"] for o in participant["orders"]))

    def test_changed_prices_and_quantities_are_calculated(self):
        catalog = {"dish": {"name": "Different dish", "unit_price_cents": 125}}
        participants = [{"id": "test", "name": "Test", "role": "guest",
                         "orders": [{"dish_id": "dish", "quantity": 3}]}]
        summary = build_summary(participants, catalog)
        self.assertEqual(summary["group_total"], 3.75)
        self.assertEqual(summary["participants"][0]["orders"][0]["line_total"], 3.75)
        participants[0]["orders"][0]["quantity"] = 4
        self.assertEqual(build_summary(participants, catalog)["group_total"], 5)

    def test_cent_arithmetic_does_not_accumulate_float_error(self):
        catalog = {"dish": {"name": "Small price", "unit_price_cents": 10}}
        participants = [{"id": str(i), "name": str(i), "role": "guest",
                         "orders": [{"dish_id": "dish", "quantity": 1}]}
                        for i in range(3)]
        self.assertEqual(build_summary(participants, catalog)["group_total"], 0.3)

    def test_empty_group(self):
        summary = build_summary([])
        self.assertEqual(summary["participants"], [])
        for field in ("group_total", "participant_count", "total_quantity", "participants_pending"):
            self.assertEqual(summary[field], 0)

    def test_input_and_subsequent_responses_are_not_mutated(self):
        before = copy.deepcopy(PARTICIPANTS)
        summary = build_summary()
        summary["participants"][0]["orders"][0]["quantity"] = 999
        self.assertEqual(PARTICIPANTS, before)
        self.assertEqual(build_summary()["group_total"], 64)

    def test_invalid_quantities_are_rejected(self):
        for quantity in (0, -1, 1.5, "2", True):
            with self.subTest(quantity=quantity), self.assertRaises(ValueError):
                build_summary([{"id": "bad", "name": "Bad", "role": "guest",
                                "orders": [{"dish_id": "strawberry-mint-salad", "quantity": quantity}]}])

    def test_invalid_prices_are_rejected(self):
        for price in (-1, 1.5, "100", True):
            with self.subTest(price=price), self.assertRaises(ValueError):
                build_summary(PARTICIPANTS, {
                    **CATALOG,
                    "strawberry-mint-salad": {"name": "Bad price", "unit_price_cents": price},
                })

    def test_unknown_dish_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "Unknown dish"):
            build_summary(PARTICIPANTS, {})


class ResponseTests(unittest.TestCase):
    def test_http_api_v2_success_response(self):
        result = handler(event(), None)
        self.assertEqual(result["statusCode"], 200)
        self.assertFalse(result["isBase64Encoded"])
        self.assertEqual(result["headers"]["Content-Type"], "application/json; charset=utf-8")
        self.assertEqual(result["headers"]["Cache-Control"], "no-store")
        self.assertEqual(json.loads(result["body"]), build_summary())

    def test_mutating_methods_and_other_methods_are_rejected(self):
        for method in ("POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"):
            with self.subTest(method=method):
                result = handler(event(method), None)
                self.assertEqual(result["statusCode"], 405)
                self.assertEqual(result["headers"]["Allow"], "GET")
                self.assertEqual(json.loads(result["body"])["error"]["code"], "method_not_allowed")

    def test_unconfigured_paths_are_rejected(self):
        for path in ("/", "/group-preorder/", "/other"):
            with self.subTest(path=path):
                self.assertEqual(handler(event(path=path), None)["statusCode"], 404)

    def test_missing_http_context_is_rejected(self):
        self.assertEqual(handler({}, None)["statusCode"], 405)

    def test_request_body_and_query_cannot_change_sample_orders(self):
        request = event()
        request["body"] = '{"quantity": 999}'
        request["queryStringParameters"] = {"quantity": "999"}
        self.assertEqual(json.loads(handler(request, None)["body"])["group_total"], 64)


if __name__ == "__main__":
    unittest.main()
