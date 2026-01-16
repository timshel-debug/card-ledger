# Transport Security Policy — CardService

## Policy Statement

**All network communication MUST use encryption in transit. TLS 1.2 or higher is mandatory for all external and internal network hops.**

## Requirements

### 1. Client to Edge (External Traffic)
- **Protocol:** HTTPS with TLS 1.2 or TLS 1.3
- **Certificate:** Valid certificate from trusted CA
- **Legacy TLS:** TLS 1.0 and 1.1 MUST be disabled

### 2. Edge to Application (Internal Traffic)

**Default (Recommended):** HTTPS Re-encrypt or mTLS
- **HTTPS Re-encrypt:** Edge load balancer/reverse proxy decrypts client TLS, then re-encrypts traffic to application using a new TLS connection
- **mTLS (Mutual TLS):** Both edge and application authenticate each other using certificates

**Exception (Requires Risk Acceptance):** TLS Termination Only
- Edge terminates TLS and sends plaintext HTTP to application
- **Conditions for exception:**
  1. Application deployed in isolated private network (VPC/VNET with strict firewall rules)
  2. No untrusted hosts share the internal network
  3. Network-level security controls verified and documented
  4. Risk formally accepted by security/engineering leadership
- **Risk:** Plaintext traffic on internal network vulnerable to MITM if network isolation is breached

### 3. Application to External Services (Outbound)
- **Protocol:** HTTPS with TLS 1.2+
- **Certificate Validation:** MUST validate server certificates against trusted CAs
- **Treasury API:** Always HTTPS (enforced by HttpClient configuration)

### 4. Database Access
- **SQLite (file-based):** Local file I/O, no network transport
- If future migration to network-based database: TLS encryption REQUIRED

## Implementation

### ASP.NET Core HTTPS Configuration

**Development:**
```csharp
// launchSettings.json
"https": {
  "commandName": "Project",
  "launchBrowser": true,
  "applicationUrl": "https://localhost:7213;http://localhost:5258",
  "environmentVariables": {
    "ASPNETCORE_ENVIRONMENT": "Development"
  }
}
```

**Production (when hosting directly):**
```csharp
// Program.cs
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
    });
});
```

**Production (behind edge/ingress with re-encrypt):**
- Configure edge to connect to application using HTTPS
- Application listens on HTTPS port with valid certificate
- No plaintext HTTP endpoints exposed

### HttpClient Configuration (Outbound)

```csharp
// Infrastructure/DependencyInjection.cs
services.AddHttpClient<ITreasuryFxRateProvider, TreasuryFxRateProvider>(client =>
{
    client.BaseAddress = new Uri(treasuryBaseUrl); // Always HTTPS
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds + 5);
})
// Default HttpClientHandler validates certificates against trusted CAs
// Never use DangerousAcceptAnyServerCertificateCallback in Production
```

## Verification

### Testing TLS Configuration
```powershell
# Verify TLS version support
curl -v --tlsv1.2 https://localhost:7213/health

# Verify certificate
openssl s_client -connect localhost:7213 -showcerts
```

### Monitoring
- Log TLS version for incoming connections (if possible at edge)
- Alert on any HTTP (non-HTTPS) traffic detected on production network

## Compliance

This policy aligns with:
- **OWASP Top 10 2021:** A02:2021 – Cryptographic Failures
- **PCI DSS 4.0:** Requirement 4.2 (Encrypt transmission of cardholder data)
- **NIST SP 800-52 Rev. 2:** TLS 1.2+ requirement

## Exceptions and Deviations

Any deviation from TLS end-to-end encryption MUST be documented and approved. Include:
1. Justification (technical/business reason)
2. Compensating controls (network isolation, firewall rules)
3. Residual risk assessment
4. Approval signatures (engineering lead, security lead)
5. Review date (annual minimum)

## References

- [HTTPS Security Boundaries Diagram](diagrams/13-security-boundaries.md#transport-security)
- [Deployment Architecture](diagrams/04-deployment.md)
- [OWASP Transport Layer Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Transport_Layer_Security_Cheat_Sheet.html)
