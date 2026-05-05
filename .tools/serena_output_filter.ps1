function Test-SerenaNoiseLine {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Line
    )

    if ($Line -eq "System.Management.Automation.RemoteException") {
        return $true
    }

    $knownNoisyMethods = @(
        "window/logMessage",
        "o#/backgrounddiagnosticstatus",
        "o#/projectdiagnosticstatus",
        "o#/projectconfiguration",
        "o#/projectadded",
        "o#/projectchanged",
        "o#/msbuildprojectdiagnostics"
    )

    foreach ($method in $knownNoisyMethods) {
        if ($Line -like "*Unhandled method '$method'*") {
            return $true
        }
    }

    if ($Line -like "*Unhandled method 'window/_roslyn_showToast'*") {
        return $true
    }

    if ($Line -like "*Unknown payload type:*Parse Error*") {
        return $true
    }

    if ($Line -like "*Error handling server payload: argument of type 'NoneType' is not iterable*") {
        return $true
    }

    # RuntimeIdentifier hint can repeat many times and does not affect indexing.
    if ($Line -like "*RuntimeIdentifier*" -and $Line -like "*doesn't list 'win'*") {
        return $true
    }

    return $false
}
