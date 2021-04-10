using Cesil.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace Cesil.Tests
{
    internal sealed class AttriberMembersGenerator : ISourceGenerator
    {
        internal AttributeTracker Tracker { get; private set; }
        internal AttributedMembers Members { get; private set; }

        public void Execute(GeneratorExecutionContext context)
        {
            Tracker = (AttributeTracker)context.SyntaxReceiver;
            Members = Tracker.GetMembers(context.Compilation);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new AttributeTracker());
        }
    }
}
