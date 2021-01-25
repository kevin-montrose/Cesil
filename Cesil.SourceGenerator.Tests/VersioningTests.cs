using System;
using System.Reflection;
using Xunit;

namespace Cesil.SourceGenerator.Tests
{
    public class VersioningTests
    {
        [Fact]
        public void CesilAssemblyVersionMatchesAheadOfTimeVersion()
        {
            var cesilAssembly = typeof(DeserializerGenerator).Assembly;
            var attrs = cesilAssembly.CustomAttributes;

            var asmFileVersionAttr = Assert.Single(attrs, a => a.AttributeType == typeof(AssemblyFileVersionAttribute));
            var asmInfoVersionAttr = Assert.Single(attrs, a => a.AttributeType == typeof(AssemblyInformationalVersionAttribute));

            var asmFileVersionStr = Assert.Single(asmFileVersionAttr.ConstructorArguments).Value as string;
            var asmInfoVersionStr = Assert.Single(asmInfoVersionAttr.ConstructorArguments).Value as string;

            var asmFileVersion = Version.Parse(asmFileVersionStr);
            var asmInfoVersion = Version.Parse(asmInfoVersionStr);
            var asmVersion = cesilAssembly.GetName().Version;

            var aotVersion = Version.Parse(AheadOfTimeTypeDescriber.CURRENT_CESIL_VERSION);
            var constVersion = Version.Parse(Constants.EXPECTED_CESIL_VERSION);

            Compare(asmVersion, aotVersion);
            Compare(asmFileVersion, asmInfoVersion);
            Compare(asmVersion, asmInfoVersion);
            Compare(aotVersion, constVersion);

            // special comparison because version have different compontents
            //   set depending on where it's from
            static void Compare(Version a, Version b)
            {
                Assert.Equal(a.Major, b.Major);
                Assert.Equal(a.Minor, b.Minor);
                Assert.Equal(a.Build, b.Build);
            }
        }
    }
}
