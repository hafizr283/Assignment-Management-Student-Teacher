namespace AssignmentSystem.Api.Services;

public static class AuthorizationRules
{
    public static bool CanTeacherManageAssignment(int teacherId, int assignmentTeacherId) =>
        teacherId == assignmentTeacherId;

    public static bool CanStudentAccessCourse(int? studentCourseId, int assignmentCourseId) =>
        studentCourseId.HasValue && studentCourseId.Value == assignmentCourseId;
}
