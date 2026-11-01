@hw8 @l1
Feature: Owner reviews a group pre-order

  @ui @smoke
  Scenario: Owner sees dishes quantities subtotals submitted and empty draft carts
    Given an isolated group pre-order in "review" state
    When the owner completes the "review" browser flow
    Then all group pre-order expectations hold

  @api
  Scenario: A newly created group contains only the empty Owner cart
    Given an isolated group pre-order in "empty" state
    When the owner checks "review" with "normal"
    Then all group pre-order expectations hold

  @api
  Scenario: Review total changes when a guest adds another dish
    Given an isolated group pre-order in "draft" state
    When the owner checks "review" with "changed cart"
    Then all group pre-order expectations hold

  @api @negative
  Scenario: A single-person reservation has no group to review
    Given an isolated group pre-order in "no-group" state
    When the owner checks "no group" with "normal"
    Then all group pre-order expectations hold

  @api @negative
  Scenario: A group without participants returns an empty review
    Given an isolated group pre-order in "zero-participants" state
    When the owner checks "review" with "normal"
    Then all group pre-order expectations hold

  @api @negative
  Scenario: Missing group cannot be reviewed
    Given an isolated group pre-order in "empty" state
    When the owner checks "missing review" with "normal"
    Then all group pre-order expectations hold

  @api @negative
  Scenario Outline: Only the Owner can review participants
    Given an isolated group pre-order in "ready" state
    When the "<persona>" attempts the protected "review" action
    Then all group pre-order expectations hold

    Examples: Unauthorised visitors
      | persona        |
      | other customer |
      | guest          |
      | anonymous      |

  @api @negative @requires-waiter
  Scenario: A real waiter cannot review Owner participants
    Given an isolated group pre-order in "ready" state
    When the "waiter" attempts the protected "review" action
    Then all group pre-order expectations hold
