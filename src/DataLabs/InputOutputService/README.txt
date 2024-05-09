How to run InputOutputSerivce locally
=======================================
InputOutput Service is a standalone .net console program. you can run it locally
However InputOutput Service is using Configuration file to get information input/output EventHubs, stroage account, serviceBus etc..

You can add any key/value pair in the LocalTestParameters.json
Key/value patterns should be same as values file in AKSDeployment folder's values file.
e.g.) AKSDeployment/Charts/PartnerAKS/testValues.yaml

For convienence, LocalTestParameters.json already has some key/value pairs. 
If you just modify existing key/values adding your own resource name and connectionstring, you can run InputOutputService locally.

InputOutputService is communicating with Partner Service which is in PartnerSolutionServiceBase project.
You have to run the Partner Service first before running InputOutputService.

Here is steps to run InputOutputService locally.
1. Open LocalTestParameters.json and modify existing key/value pairs adding your own resource name and connectionstring.
2. Run Partner Service first. (simply right click on the PartnerSolutionServiceBase project and select "Debug" -> "Start Without Debugging")
3. Run InputOutputService. You can set breakpoint in the code to debug.