@hw8 @l3
Feature: Owner removes a guest before sending the pre-order

  @ui
  Scenario: Removing a submitted guest recalculates the browser Grand Total
    Given an isolated group pre-order in "ready" state
    When the owner completes the "remove" browser flow
    Then all group pre-order expectations hold

  @api
  Scenario: A submitted guest is removed and cannot be removed twice
    Given an isolated group pre-order in "ready" state
    When the owner checks "remove" with "normal"
    Then all group pre-order expectations hold

  @api
  Scenario: The last Guest can be removed while the Owner remains
    Given an isolated group pre-order in "draft" state
    When the owner checks "remove" with "normal"
    Then all group pre-order expectations hold

  @api @negative
  Scenario Outline: Invalid participants cannot be removed
    Given an isolated group pre-order in "ready" state
    When the owner checks "remove" with "<participant>"
    Then all group pre-order expectations hold

    Examples: Protected and missing participants
      | participant |
      | owner       |
      | missing     |
      | foreign     |

  @api @negative
  Scenario Outline: Only the Owner can remove a guest
    Given an isolated group pre-order in "ready" state
    When the "<persona>" attempts the protected "remove" action
    Then all group pre-order expectations hold

    Examples: Unauthorised visitors
      | persona        |
      | other customer |
      | guest          |
      | anonymous      |

  @api @negative @requires-waiter
  Scenario: A real waiter cannot remove an Owner guest
    Given an isolated group pre-order in "ready" state
    When the "waiter" attempts the protected "remove" action
    Then all group pre-order expectations hold

  @ui @e2e @trace-always
  Scenario: Owner and invited guest order remove a guest and send the final order
    Given an isolated group pre-order in "empty" state
    When the owner completes the "journey" browser flow
    Then all group pre-order expectations hold
