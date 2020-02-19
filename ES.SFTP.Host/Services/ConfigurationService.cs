using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ES.SFTP.Host.Business.Configuration;
using ES.SFTP.Host.Messages.Configuration;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ES.SFTP.Host.Services
{
    public class ConfigurationService : IRequestHandler<SftpConfigurationRequest,SftpConfiguration>, IRequestHandler<SftpConfigurationLoadRequest>
    {
        private readonly ILogger<Orchestrator> _logger;
        private readonly IOptionsMonitor<SftpConfiguration> _sftpOptionsMonitor;
        private SftpConfiguration _configuration;

        public ConfigurationService(ILogger<Orchestrator> logger,
            IOptionsMonitor<SftpConfiguration> sftpOptionsMonitor)
        {
            _logger = logger;
            _sftpOptionsMonitor = sftpOptionsMonitor;
            _sftpOptionsMonitor.OnChange((_, __) =>
            {
                _logger.LogWarning("Configuration changed.");
            });
        }


        public Task<SftpConfiguration> Handle(SftpConfigurationRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_configuration);
        }

        public Task<Unit> Handle(SftpConfigurationLoadRequest request, CancellationToken cancellationToken)
        {
            if (_configuration != null) return Unit.Task;

            _logger.LogDebug("Preparing and validating configuration");

            var config = _sftpOptionsMonitor.CurrentValue ?? new SftpConfiguration();

            config.Global ??= new GlobalConfiguration();

            config.Global.Directories ??= new List<string>();
            config.Global.Logging ??= new LoggingDefinition();
            config.Global.Chroot ??= new ChrootDefinition();
            if (string.IsNullOrWhiteSpace(config.Global.Chroot.Directory)) config.Global.Chroot.Directory = "%h";
            if (string.IsNullOrWhiteSpace(config.Global.Chroot.StartPath)) config.Global.Chroot.StartPath = null;


            config.Users ??= new List<UserDefinition>();

            var validUsers = new List<UserDefinition>();
            for (var index = 0; index < config.Users.Count; index++)
            {
                var userDefinition = config.Users[index];
                if (string.IsNullOrWhiteSpace(userDefinition.Username))
                {
                    _logger.LogWarning("Users[index] has a null or whitespace username. Skipping user.", index);
                    continue;
                }

                userDefinition.Chroot ??= new ChrootDefinition();
                if (string.IsNullOrWhiteSpace(userDefinition.Chroot.Directory))
                    userDefinition.Chroot.Directory = config.Global.Chroot.Directory;
                if (string.IsNullOrWhiteSpace(userDefinition.Chroot.StartPath))
                    userDefinition.Chroot.StartPath = config.Global.Chroot.StartPath;

                if (userDefinition.Chroot.Directory == config.Global.Chroot.Directory &&
                    userDefinition.Chroot.StartPath == config.Global.Chroot.StartPath)
                    userDefinition.Chroot = null;
                userDefinition.Directories ??= new List<string>();

                validUsers.Add(userDefinition);
            }

            config.Users = validUsers;
            _logger.LogInformation("Configuration contains '{userCount}' user(s)", config.Users.Count);

            _configuration = config;
            return Unit.Task;
        }
    }
}
