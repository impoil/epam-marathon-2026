@hw8 @l2
Feature: Owner sends a consolidated pre-order to the restaurant

  @ui
  Scenario: Browser submission excludes draft dishes and becomes read-only
    Given an isolated group pre-order in "ready" state
    When the owner completes the "submit" browser flow
    Then all group pre-order expectations hold

  @api @smoke
  Scenario: Matching dishes aggregate and a second submission is refused
    Given an isolated group pre-order in "ready" state
    When the owner checks "submit" with "success"
    Then all group pre-order expectations hold

  @api @requires-waiter
  Scenario: Assigned waiter sees the same submitted order as the Owner
    Given an isolated group pre-order in "ready" state
    When the owner checks "waiter visibility" with "success"
    Then all group pre-order expectations hold

  @api @negative
  Scenario Outline: Incomplete group submissions explain why they are refused
    Given an isolated group pre-order in "<state>" state
    When the owner checks "submit" with "<problem>"
    Then all group pre-order expectations hold

    Examples: Missing prerequisites
      | state            | problem             |
      | draft            | no submitted carts  |
      | empty-submitted  | no dishes           |
      | missing-order    | missing order       |
      | ready            | missing group       |
      | ready            | missing reservation |
      | ready            | invalid reservation |

  @api @negative
  Scenario Outline: Only the Owner can submit the group
    Given an isolated group pre-order in "ready" state
    When the "<persona>" attempts the protected "submit" action
    Then all group pre-order expectations hold

    Examples: Unauthorised visitors
      | persona        |
      | other customer |
      | guest          |
      | anonymous      |

  @api @negative @requires-waiter
  Scenario: A real waiter cannot submit the Owner group
    Given an isolated group pre-order in "ready" state
    When the "waiter" attempts the protected "submit" action
    Then all group pre-order expectations hold
