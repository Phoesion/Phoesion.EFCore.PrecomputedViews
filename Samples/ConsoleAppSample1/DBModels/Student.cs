using Microsoft.EntityFrameworkCore;
using Phoesion.EFCore.PrecomputedViews;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ConsoleAppSample1.DBModels
{
    [DependsOn<Enrollment>(DependencyEvents.Added | DependencyEvents.Removed, typeof(EnrollmentAddedRemovedHandler))]
    public class Student
    {
        [Key]
        public int Id { get; set; }

        public string LastName { get; set; }
        public string FirstMidName { get; set; }
        public DateTime EnrollmentDate { get; set; }

        public ICollection<Enrollment> Enrollments { get; set; }

        //have a pre-computed count of enrollments (this can be a read-heavy and compute-heavy count that we want to have pre-computed)
        public int EnrollmentCount { get; set; }


        //=======================
        // View handlers
        //=======================
        class EnrollmentAddedRemovedHandler : IComputableView<dbContext, Enrollment>
        {
            public async ValueTask ComputeView(dbContext context, DependencyEvents ev, IEnumerable<Enrollment> changedDependencies)
            {
                //find students that are affected
                var studentIdsAffected = changedDependencies?.Select(x => x.StudentId);

                // limit search space to affected students only (otherwise the query will run on all student entities)
                var entities = studentIdsAffected != null ?
                                        context.Students.Where(s => studentIdsAffected.Contains(s.Id)) :
                                        context.Students;

                //execute update 
                await entities.ExecuteUpdateAsync(e => e.SetProperty(
                                x => x.EnrollmentCount,
                                x => context.Enrollments
                                                .Where(enroll => enroll.StudentId == x.Id)
                                                .Count()));
            }
        }
    }
}
