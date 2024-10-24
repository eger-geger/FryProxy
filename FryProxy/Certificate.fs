module FryProxy.Certificate

open System
open System.Security.Cryptography
open System.Security.Cryptography.X509Certificates

/// Generate self-signed certificate with given name and lifetime.
let SelfSigned(name: X500DistinguishedName, lifetime: TimeSpan) =
    let start = DateTimeOffset.UtcNow

    let req =
        CertificateRequest(name, RSA.Create(), HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1)

    let cert = req.CreateSelfSigned(start, start + lifetime)
    
    // add certificate to windows keystore as side effect
    new X509Certificate2(cert.Export(X509ContentType.Pfx)) 

/// Generate self-signed certificate valid for a year.
let ProxyDefault () =
    let dnb = X500DistinguishedNameBuilder()
    dnb.AddOrganizationName("egergeger")
    dnb.AddOrganizationalUnitName("FryProxy")
    dnb.AddCountryOrRegion("UA")
    dnb.AddLocalityName("Uzhhorod")
    dnb.AddStateOrProvinceName("Zakarpattia")
    dnb.AddCommonName("localhost")
    dnb.AddDomainComponent("localhost")

    SelfSigned(dnb.Build(), TimeSpan.FromDays(365))
