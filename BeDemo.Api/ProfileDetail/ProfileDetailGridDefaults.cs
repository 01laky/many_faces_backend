namespace BeDemo.Api.ProfileDetail;

/// <summary>
/// Default member profile detail grid per face (PageType <c>profileDetail</c>).
/// Keep in sync with <c>many_faces_portal/src/features/profileDetail/schema/defaultProfileDetailSchema.ts</c>.
/// </summary>
public static class ProfileDetailGridDefaults
{
    public const string PageTypeIndex = "profileDetail";

    public const string TemplatePagePath = "/_profile-detail";

    public const string TemplatePageName = "Member profile layout";

    public const int TemplatePageSortIndex = 999;

    public const string DefaultGridSchemaJson =
        """
        {"schemaVersion":1,"rowHeight":80,"breakpoints":{"lg":1200,"md":996,"sm":768,"xs":480,"xxs":0},"cols":{"lg":12,"md":10,"sm":6,"xs":4,"xxs":2},"items":[{"i":"hero","x":0,"y":0,"w":12,"h":5,"sectionType":"profileHero","props":{"variant":"centered","includeMeta":true,"includeLike":true}},{"i":"comments","x":0,"y":5,"w":12,"h":6,"sectionType":"profileComments"},{"i":"reviews","x":0,"y":11,"w":12,"h":6,"sectionType":"profileReviews","props":{"showRecensionsDisabledMessage":true,"hideWhenRecensionsDisabled":false}}]}
        """;
}
