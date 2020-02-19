using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ES.SFTP.Host.Business.Configuration;
using MediatR;

namespace ES.SFTP.Host.Messages.Configuration
{
    public class SftpConfigurationRequest:IRequest<SftpConfiguration>
    {
    }

    public class SftpConfigurationLoadRequest : IRequest
    {
    }
}
