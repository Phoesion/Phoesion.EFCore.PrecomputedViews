using Phoesion.EFCore.PrecomputedViews;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ConsoleAppSample1.DBModels
{
    public class Enrollment
    {
        [Key]
        public int Id { get; set; }

        public int CourseId { get; set; }
        public Course Course { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; }
    }
}
