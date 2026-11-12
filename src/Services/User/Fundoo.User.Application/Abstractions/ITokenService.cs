using Fundoo.User.Domain.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.User.Application.Abstractions
{
    public interface ITokenService
    {
        string CreateAccessToken(AppUser user);
    }
}
