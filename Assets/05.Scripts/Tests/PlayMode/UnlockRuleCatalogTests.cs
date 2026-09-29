using System;
using System.Collections;
using NUnit.Framework;

namespace ProjectS.Tests.PlayMode
{
    public sealed class UnlockRuleCatalogTests
    {
        private static readonly Type ParserType = GetGameplayType("ProjectS.Unlocks.UnlockRuleCsvParser");

        [Test]
        public void HeaderOnlyTemplateImportsNoRules()
        {
            var csv = "규칙ID,대상종류,대상ID,필요건물,필요연구,선행해금,활성,메모\r\n";
            var entries = (Array)InvokeStatic(ParserType, "Parse", csv);

            Assert.That(entries.Length, Is.Zero);
        }

        [Test]
        public void InactiveRulePreservesAllEditableRequirementColumns()
        {
            var csv =
                "규칙ID,대상종류,대상ID,필요건물,필요연구,선행해금,활성,메모\r\n"
                + "scout-rule,유닛,Scout,SignalRelay|Production,recon-research,field-recon,FALSE,비활성 입력 예시\r\n";
            var entries = (Array)InvokeStatic(ParserType, "Parse", csv);
            var entry = entries.GetValue(0);

            Assert.That(entries.Length, Is.EqualTo(1));
            Assert.That(GetProperty(entry, "RuleId"), Is.EqualTo("scout-rule"));
            Assert.That(GetProperty(entry, "TargetType").ToString(), Is.EqualTo("Unit"));
            Assert.That(GetProperty(entry, "TargetId"), Is.EqualTo("Scout"));
            CollectionAssert.AreEqual(
                new[] { "SignalRelay", "Production" },
                (ICollection)GetProperty(entry, "RequiredBuildings"));
            CollectionAssert.AreEqual(
                new[] { "recon-research" },
                (ICollection)GetProperty(entry, "RequiredResearchIds"));
            CollectionAssert.AreEqual(
                new[] { "field-recon" },
                (ICollection)GetProperty(entry, "PrerequisiteUnlockIds"));
            Assert.That(GetProperty(entry, "Active"), Is.False);
        }

        private static Type GetGameplayType(string typeName)
        {
            var type = Type.GetType($"{typeName}, Assembly-CSharp");
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.That(type, Is.Not.Null, $"Could not resolve gameplay type {typeName}.");
            return type;
        }

        private static object InvokeStatic(Type targetType, string methodName, params object[] arguments)
        {
            var method = targetType.GetMethod(methodName);
            Assert.That(method, Is.Not.Null, $"Could not resolve method {targetType.Name}.{methodName}.");
            return method.Invoke(null, arguments);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Could not resolve property {target.GetType().Name}.{propertyName}.");
            return property.GetValue(target);
        }
    }
}
