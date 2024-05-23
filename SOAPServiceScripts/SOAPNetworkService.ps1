param([String] $SetName1 = $(throw "Please specify the set name"),
      [String] $SetUserName1 = $(throw "Please specify the set username"))

function Get-LanDbSet([string] $SetName) {
		Import-Module CredentialManager
		$cred = Get-StoredCredential -Target "svcgtw"
		$service = New-WebServiceProxy -uri "https://network.cern.ch/sc/soap/soap.fcgi?v=6&WSDL" -Namespace SOAPNetworkService -Class SOAPNetworkServiceClass
		$service.AuthValue = New-Object SOAPNetworkService.Auth
		$service.AuthValue.token = $service.getAuthToken($cred.UserName, $cred.GetNetworkCredential().Password, "NICE")

                # Call to recursive function for set exploration - output file containing IP addresses at C:\temp.txt
                $DeviceInfo = $service.getDeviceInfo($SetName)
                $DeviceInfo | Add-Member -MemberType NoteProperty -Name NetworkDomainName -Value $DeviceInfo.Interfaces.NetworkDomainName
                $DeviceInfo | Add-Member -MemberType NoteProperty -Name ResponsiblePersonName -Value $DeviceInfo.ResponsiblePerson.Name
                $DeviceInfo | Add-Member -MemberType NoteProperty -Name ResponsiblePersonEmail -Value $DeviceInfo.ResponsiblePerson.Email

                $OwnerInfo=Get-ADUser -Filter {mail -eq $DeviceInfo.ResponsiblePerson.Email}
                $DeviceInfo | Add-Member -MemberType NoteProperty -Name ResponsiblePersonUsername -Value $OwnerInfo.Name
                $DeviceInfo | Add-Member -MemberType NoteProperty -Name UsersName -Value $DeviceInfo.UserPerson.FirstName
                $DeviceInfo | Add-Member -MemberType NoteProperty -Name UsersSurname -Value $DeviceInfo.UserPerson.Name
		try {
                	if ($SetUserName1 -ne "null") {
                    	$UserInfo = Get-ADUser -Identity $SetUserName1 -Properties givenName
                    	$DeviceInfo | Add-Member -MemberType NoteProperty -Name UserGivenName -Value $UserInfo.givenName
                	}
		}
		catch {
			$DeviceInfo | Add-Member -MemberType NoteProperty -Name UserGivenName -Value $SetUserName1
		}

                return $DeviceInfo
}

# example call to Get-LanDBSet:
$result = Get-LanDbSet -SetName $SetName1
$result