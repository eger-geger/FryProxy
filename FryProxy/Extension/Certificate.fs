[<AutoOpen>]
module FryProxy.Extension.Certificate

open System
open System.Security.Cryptography
open System.Security.Cryptography.X509Certificates

type X509Certificate with
    
    /// Generate self-signed certificate with given name and lifetime.
    static member inline SelfSigned(name: X500DistinguishedName, lifetime: TimeSpan) =
        let start = DateTimeOffset.UtcNow

        let req =
            CertificateRequest(name, RSA.Create(), HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1)

        let cert = req.CreateSelfSigned(start, start + lifetime)
        
        // add certificate to windows keystore as side effect
        new X509Certificate2(cert.Export(X509ContentType.Pfx)) 
    
    /// Create default proxy self-signed certificate valid for a year.
    static member inline ProxyDefault =
        let dnb = X500DistinguishedNameBuilder()
        dnb.AddOrganizationName("egergeger")
        dnb.AddOrganizationalUnitName("FryProxy")
        dnb.AddCountryOrRegion("UA")
        dnb.AddLocalityName("Uzhhorod")
        dnb.AddStateOrProvinceName("Zakarpattia")
        dnb.AddCommonName("localhost")
        dnb.AddDomainComponent("localhost")

        X509Certificate.SelfSigned(dnb.Build(), TimeSpan.FromDays(365))
