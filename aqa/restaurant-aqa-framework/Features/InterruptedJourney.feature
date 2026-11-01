@continuation @ui @trace-always
Feature: Finish the same interrupted scenario without another reservation

  Scenario: Continue the interrupted Owner and Guest browser journey on its own group
    Given the interrupted group from this run is continued without another reservation
    When the owner completes the "journey" browser flow
    Then all group pre-order expectations hold
