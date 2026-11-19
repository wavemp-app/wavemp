using AwesomeAssertions;
using WaveMP.Core.Playlists.SmartPlaylists;

namespace WaveMP.Core.Playlists.Tests.SmartPlaylists;

[TestFixture]
public class SmartPlaylistTemplatesTests
{
    [Test]
    public void All_EveryTemplateOnce_InTheOrderOfTheIds()
    {
        SmartPlaylistTemplates.All.Select(template => template.Id)
            .Should().Equal(Enum.GetValues<SmartPlaylistTemplateId>());
    }

    [TestCaseSource(nameof(TemplateIds))]
    public void All_EveryTemplate_PassesTheValidationUnchanged(SmartPlaylistTemplateId id)
    {
        SmartPlaylistTemplate template = SmartPlaylistTemplates.Get(id);

        SmartPlaylistValidation validation = SmartPlaylistRules.Validate(new SmartPlaylistDefinition(
            "Template",
            template.Criteria.Match,
            template.Criteria.Rules,
            template.Criteria.Sort,
            template.Criteria.Direction,
            template.Criteria.Limit));

        validation.IsValid.Should().BeTrue();
        validation.Criteria.Rules.Should().Equal(template.Criteria.Rules);
    }

    [Test]
    public void Get_UnknownId_Throws()
    {
        Action get = () => SmartPlaylistTemplates.Get((SmartPlaylistTemplateId)99);

        get.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static IEnumerable<SmartPlaylistTemplateId> TemplateIds()
    {
        return Enum.GetValues<SmartPlaylistTemplateId>();
    }
}
