﻿param([String] $SetName1 = $(throw "Please specify the set name"),
      [String] $SetUserName1 = $(throw "Please specify the set username"))

function Get-LanDbSet([string] $SetName,[String] $UserName,[String] $Password) {
                $service=New-WebServiceProxy -uri "https://network.cern.ch/sc/soap/soap.fcgi?v=6&WSDL" -Namespace SOAPNetworkService -Class SOAPNetworkServiceClass
                $service.AuthValue = new-object SOAPNetworkService.Auth
                $service.AuthValue.token = $service.getAuthToken($UserName,$Password,"NICE")
                # Call to recursive function for set exploration - output file containing IP addresses at C:\temp.txt
                $DeviceInfo = $service.getDeviceInfo($SetName)
                $DeviceInfo | Add-Member -MemberType NoteProperty -Name NetworkDomainName -Value $DeviceInfo.Interfaces.NetworkDomainName
                $DeviceInfo | Add-Member -MemberType NoteProperty -Name ResponsiblePersonName -Value $DeviceInfo.ResponsiblePerson.Name
                $DeviceInfo | Add-Member -MemberType NoteProperty -Name ResponsiblePersonEmail -Value $DeviceInfo.ResponsiblePerson.Email

                $OwnerInfo=Get-ADUser -Filter {mail -eq $DeviceInfo.ResponsiblePerson.Email}
                $DeviceInfo | Add-Member -MemberType NoteProperty -Name ResponsiblePersonUsername -Value $OwnerInfo.Name
                $DeviceInfo | Add-Member -MemberType NoteProperty -Name UsersName -Value $DeviceInfo.UserPerson.FirstName
                $DeviceInfo | Add-Member -MemberType NoteProperty -Name UsersSurname -Value $DeviceInfo.UserPerson.Name

                if ($SetUserName1 -ne "null") {
                    $UserInfo = Get-ADUser -Identity $SetUserName1 -Properties givenName
                    $DeviceInfo | Add-Member -MemberType NoteProperty -Name UserGivenName -Value $UserInfo.givenName
                }




                return $DeviceInfo
}

# example call to Get-LanDBSet:
$result = Get-LanDbSet -SetName $SetName1 -UserName "pstojkov" -Password "GeForce9800GT."
$result