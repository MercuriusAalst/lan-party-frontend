# Auth0 Setup

Configure the Auth0 application as a Regular Web Application.

## Application URLs

Allowed Callback URLs:

- `https://localhost:7044/callback`
- `http://localhost:5003/callback`
- `https://lan.mercurius-aalst.be/callback`

Allowed Logout URLs:

- `https://localhost:7044/account/logout/callback`
- `http://localhost:5003/account/logout/callback`
- `https://lan.mercurius-aalst.be/account/logout/callback`

The application always returns from Auth0 through the fixed
`/account/logout/callback` path and appends the already validated destination as
an encoded `returnUrl` query parameter. Auth0's Allowed Logout URL validation
does not take the query string or hash into account; see the official
[Redirect Users After Logout documentation](https://auth0.com/docs/authenticate/login/logout/redirect-users-after-logout).
The exact base callback URL for each origin is therefore sufficient; do not add
dynamic paths or query-string variants. The callback revalidates the decoded
target and falls back to `/` when it is missing, unsafe, protected, or
malformed.

## Application Settings

Keep `Auth0:ClientSecret` out of committed configuration. Use user-secrets locally:

```powershell
dotnet user-secrets set "Auth0:Domain" "auth.mercurius-aalst.be" --project src\Mercurius.LAN.Web
dotnet user-secrets set "Auth0:ClientId" "<client-id>" --project src\Mercurius.LAN.Web
dotnet user-secrets set "Auth0:ClientSecret" "<client-secret>" --project src\Mercurius.LAN.Web
dotnet user-secrets set "Auth0:Audience" "<api-audience>" --project src\Mercurius.LAN.Web
```

Use environment variables for deployed environments.

## Roles Claim Action

Create a Post-Login Action and bind it to the Login flow. Keep the Auth0 role name lowercase `admin`.

```javascript
exports.onExecutePostLogin = async (event, api) => {
  const namespace = 'https://mercurius-aalst.be/roles';
  const roles = event.authorization?.roles || [];

  api.idToken.setCustomClaim(namespace, roles);
  api.accessToken.setCustomClaim(namespace, roles);
};
```
