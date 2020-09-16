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
            var user = GetClaimsPrincipal(loginData);
            return await Task.FromResult(new AuthenticationState(user));
        }

        public void MarkUserAsAuthenticated(LoginData loginData)
        {
            var user = GetClaimsPrincipal(loginData);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        ClaimsPrincipal GetClaimsPrincipal(LoginData loginData)
        {
            if (loginData == null)
                return new ClaimsPrincipal();

            var identity = new ClaimsIdentity(new[]
            {
                new Claim("id", loginData.StaffId.ToString()),
                new Claim("role_id", loginData.StaffRoleId.ToString()),
                new Claim("name", $"{loginData.StaffFirstName} {loginData.StaffLastName}")
            }, "basic");
            var user = new ClaimsPrincipal(identity);
            return user;
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
