using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ES.SFTP.Host.Business.Interop;
using ES.SFTP.Host.Messages.Authentication;
using MediatR;

namespace ES.SFTP.Host.Services
{
    public class SecurityService : IRequestHandler<SecurityServiceStartRequest>
    {
        public SecurityService()
        {

        }

        [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
        public async Task<Unit> Handle(SecurityServiceStartRequest request, CancellationToken cancellationToken)
        {
            const string pamDirPath = "/etc/pam.d";
            const string pamHookName = "sftp-hook";
            var pamCommonSessionFile = Path.Combine(pamDirPath, "common-session");
            var pamSftpHookFile = Path.Combine(pamDirPath, pamHookName);


            await ProcessUtil.QuickRun("service", "sssd stop", false);

            File.Copy("./config/sssd.conf", "/etc/sssd/sssd.conf", true);
            await ProcessUtil.QuickRun("chown", "root:root \"/etc/sssd/sssd.conf\"");
            await ProcessUtil.QuickRun("chmod", "600 \"/etc/sssd/sssd.conf\"");


            var scriptsDirectory = Path.Combine(pamDirPath, "scripts");
            if (!Directory.Exists(scriptsDirectory)) Directory.CreateDirectory(scriptsDirectory);
            var hookScriptFile = Path.Combine(new DirectoryInfo(scriptsDirectory).FullName, "sftp-pam-event.sh");
            var eventsScriptBuilder = new StringBuilder();
            eventsScriptBuilder.AppendLine("#!/bin/sh");
            eventsScriptBuilder.AppendLine(
                "curl \"http://localhost/api/events/pam/generic?username=$PAM_USER&type=$PAM_TYPE&service=$PAM_SERVICE\"");
            await File.WriteAllTextAsync(hookScriptFile, eventsScriptBuilder.ToString());
            await ProcessUtil.QuickRun("chown", $"root:root \"{hookScriptFile}\"");
            await ProcessUtil.QuickRun("chmod", $"+x \"{hookScriptFile}\"");


            var hookBuilder = new StringBuilder();
            hookBuilder.AppendLine("# This file is used to signal the SFTP service on user events.");
            hookBuilder.AppendLine($"session required pam_exec.so {new FileInfo(hookScriptFile).FullName}");
            await File.WriteAllTextAsync(pamSftpHookFile, hookBuilder.ToString());
            await ProcessUtil.QuickRun("chown", $"root:root \"{pamSftpHookFile}\"");
            await ProcessUtil.QuickRun("chmod", $"644 \"{pamSftpHookFile}\"");


            if (!(await File.ReadAllTextAsync(pamCommonSessionFile)).Contains($"@include {pamHookName}"))
                await File.AppendAllTextAsync(pamCommonSessionFile, $"@include {pamHookName}{Environment.NewLine}");


            await ProcessUtil.QuickRun("service", "sssd restart", false);

            return Unit.Value;
        }
    }
}
