using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class TeamTutorialCatalogTests
{
    [Test]
    public void CatalogContainsFoundationAndExactlySixTeamCourses()
    {
        IReadOnlyList<TeamTutorialCourseDefinition> courses = TeamTutorialCatalog.All;

        Assert.That(courses.Count, Is.EqualTo(7));
        Assert.That(courses[0].Kind, Is.EqualTo(TutorialCourseKind.Foundation));
        Assert.That(courses[0].IsPlayable, Is.True);
        CollectionAssert.AreEquivalent(
            new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP },
            courses.Where(course => course.HasTeam).Select(course => course.Team));
    }

    [Test]
    public void CourseIdsAndTeamEntriesAreUniqueAndComplete()
    {
        IReadOnlyList<TeamTutorialCourseDefinition> courses = TeamTutorialCatalog.All;

        Assert.That(courses.Select(course => course.Id).Distinct().Count(), Is.EqualTo(courses.Count));
        foreach (TeamTutorialCourseDefinition course in courses)
        {
            Assert.That(course.DisplayName, Is.Not.Empty, course.Id);
            Assert.That(course.Summary, Is.Not.Empty, course.Id);
            Assert.That(course.RecommendedTrackId, Is.Not.Empty, course.Id);
            Assert.That(course.Lessons.Count, Is.GreaterThanOrEqualTo(3), course.Id);
            foreach (TeamTutorialLessonDefinition lesson in course.Lessons)
            {
                Assert.That(lesson.Title, Is.Not.Empty, course.Id);
                Assert.That(lesson.Mechanic, Is.Not.Empty, lesson.Title);
                Assert.That(lesson.PlayerAction, Is.Not.Empty, lesson.Title);
                Assert.That(lesson.SuccessSignal, Is.Not.Empty, lesson.Title);
            }
        }

        foreach (TeamId team in new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP })
            Assert.That(TeamTutorialCatalog.GetForTeam(team), Is.Not.Null, team.ToString());
    }

    [Test]
    public void ChinaAndJapanCoursesDescribeCurrentRuntimeMechanicsOnly()
    {
        TeamTutorialCourseDefinition china = TeamTutorialCatalog.GetForTeam(TeamId.CN);
        string chinaText = string.Join(" ", china.Lessons.Select(lesson => lesson.Mechanic));
        StringAssert.Contains("首次 Go", chinaText);
        StringAssert.Contains("Recover", chinaText);
        StringAssert.Contains("不计入弯道限速", chinaText);
        StringAssert.DoesNotContain("电池衰减", chinaText);

        TeamTutorialCourseDefinition japan = TeamTutorialCatalog.GetForTeam(TeamId.JP);
        string japanText = string.Join(" ", japan.Lessons.Select(lesson => lesson.Mechanic));
        StringAssert.Contains("鱼雷天妇罗", japanText);
        StringAssert.Contains("关东慢煮", japanText);
        StringAssert.DoesNotContain("随机秘方牌池", japanText);
    }
}
