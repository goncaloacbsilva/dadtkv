# DADKTV

Distributed transactional key-value store to manage data objects

How to run the project

first you have to go to controlPlane-program.cs to change the files directory of the executables to your own:

dotnet run --project <ControlPlane path> <config path> <view mode> <logLevel level>

LogLevels:
INFO
DEBUG

There are four view modes:<br/>
&emsp;&ensp;TRANSACTION to open individual windows for each transaction manager <br/>
&emsp;&ensp;LEASE to open individual windows for each lease manager<br/>
&emsp;&ensp;pCLIENT to open individual windows for each client<br/>
&emsp;&ensp;ALL to open individual windows for all the processes<br/>
