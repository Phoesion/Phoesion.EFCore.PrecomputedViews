using Microsoft.EntityFrameworkCore;
using Phoesion.EFCore.PrecomputedViews;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;

namespace ConsoleAppSample1.DBModels
{
    [DependsOn<Enrollment>(DependencyEvents.Added | DependencyEvents.Removed, typeof(EnrollmentAddedRemovedHandler))]
    public class Course
    {
        [Key]
        public int Id { get; set; }

        public string Title { get; set; }

        public ICollection<Enrollment> Enrollments { get; set; }

        //This will point to the latest Enrollment added, without needing to update it manually, using the CourseViewEnrollmentAddedHandler.ComputeView() method.
        public int? LatestEnrollmentIdAddedId { get; set; }
        public Enrollment? LatestEnrollmentIdAdded { get; set; }


        //=======================
        // View handlers
        //=======================
        class EnrollmentAddedRemovedHandler : IComputableView<dbContext, Enrollment>
        {
            public async ValueTask ComputeView(dbContext context, DependencyEvents ev, IEnumerable<Enrollment> changedDependencies)
            {
                //find courses that are affected
                var courseIdsAffected = changedDependencies.Select(x => x.CourseId);

                //execute update
                await context.Courses
                                .Where(c => courseIdsAffected.Contains(c.Id))     // limit search space to affected courses only (otherwise the query will update all course entries)
                                .ExecuteUpdateAsync(e => e.SetProperty(
                                    x => x.LatestEnrollmentIdAddedId,
                                    x => context.Enrollments
                                                    .Where(enroll => x.Id == enroll.CourseId)
                                                    .OrderByDescending(enroll => enroll.Id)
                                                    .Select(enroll => (int?)enroll.Id)
                                                    .FirstOrDefault()));
            }
        }
    }
}
