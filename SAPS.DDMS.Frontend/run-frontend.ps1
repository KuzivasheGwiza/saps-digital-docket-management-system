$ErrorActionPreference = "Stop"
$env:NG_CLI_ANALYTICS = "false"
npm.cmd run start -- --host 127.0.0.1 --port 4200 *> ng-serve.combined.log
