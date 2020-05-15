using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdateConsole.Models
{
    public class AuthenStateProvider : AuthenticationStateProvider
    {
        private ISessionStorageService _sessionStorage;

        public AuthenStateProvider(ISessionStorageService sessionStorage)
        {
            _sessionStorage = sessionStorage;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var loginData = await _sessionStorage.GetItemAsync<LoginData>("LoginData");
            ClaimsIdentity identity;
            if (loginData != null)
            {
                identity = new ClaimsIdentity(new[]
                {
                        new Claim(ClaimTypes.Name, loginData.UserName),
                }, "user_auth_type");
            }
            else
            {
                identity = new ClaimsIdentity();
            }
            var user = new ClaimsPrincipal(identity);
            return await Task.FromResult(new AuthenticationState(user));
        }

        public void MarkUserAsAuthenticated(LoginData loginData)
        {
            var identity = new ClaimsIdentity(new[]
                { new Claim(ClaimTypes.Name, loginData.UserName),
                }, "user_auth_type");
            var user = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        public async Task MarkUserAsLoggedOut()
        {
            await _sessionStorage.RemoveItemAsync("LoginData");
            var identity = new ClaimsIdentity();
            var user = new ClaimsPrincipal(identity);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }
    }
}
