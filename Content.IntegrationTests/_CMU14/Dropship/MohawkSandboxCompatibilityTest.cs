using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Content.Shared.CMU14.Dropship.DirectFire;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkSandboxCompatibilityTest
{
    [Test]
    public void SharedAssemblyDoesNotReferenceSandboxForbiddenListHelpers()
    {
        using var stream = File.OpenRead(typeof(GunshipDirectFireSystem).Assembly.Location);
        using var assembly = new PEReader(stream);
        var metadata = assembly.GetMetadataReader();
        foreach (var handle in metadata.MemberReferences)
        {
            var member = metadata.GetMemberReference(handle);
            if (metadata.GetString(member.Name) != "SetCount" || member.Parent.Kind != HandleKind.TypeReference)
                continue;

            var type = metadata.GetTypeReference((TypeReferenceHandle) member.Parent);
            Assert.That(metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name),
                Is.Not.EqualTo("System.Runtime.InteropServices.CollectionsMarshal"),
                "Nonempty List collection expressions emit a helper forbidden by the client sandbox; use an explicit List initializer.");
        }
    }
}
