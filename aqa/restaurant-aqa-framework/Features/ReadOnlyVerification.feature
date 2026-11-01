@diagnostic @nonacceptance @readonly @ui @trace-always
Feature: Diagnose a retained test group without creating more records

  Scenario: Owner reads only the group recorded in a local HW8 lifecycle manifest
    Given a retained group from this run is opened without changing it
    When the owner completes the "verify" browser flow
    Then all group pre-order expectations hold
