// Copyright (c) 2012-2020 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System;

namespace DicomServerWorkList.Model
{

    /// <summary>
    /// This class contains the most important values that are transmitted per worklist
    /// </summary>
    [Serializable]
    public class WorklistItem
    {

        public string AccessionNumber { get; set; }

        public string PatientID { get; set; }

        public string PatientName { get; set; }
        public string GetAgeString()
        {
            var days = (DateTime.Now - DateOfBirth).Days;
            if (days < 365) return $"{Convert.ToInt32(days / 12)} MOIS";
            else return $"{Convert.ToInt32(days / 365)} ANS";
        }
        public string GetAge()
        {
            var days = (DateTime.Now - DateOfBirth).Days;
            var age = 0;
            if (days < 365)
            {
                age = Convert.ToInt32(days / 12);
                return $"{age.ToString().PadLeft(3, '0')}M";
            }
            else
            {
                age = Convert.ToInt32(days / 365);
                return $"{age.ToString().PadLeft(3, '0')}Y";
            }         
        }

        //public string Forename { get; set; }

        //public string Title { get; set; }

        public string Sex { get; set; }

        public DateTime DateOfBirth { get; set; }

        public string ReferringPhysician { get; set; }

        public string PerformingPhysician { get; set; }

        public string Modality { get; set; } = "CT";

        public DateTime ExamDateAndTime { get; set; }

        //public string ExamRoom { get; set; }

        public string ExamDescription { get; set; }

        public string StudyUID { get; set; }
        public string StudyID { get; set; }

        public string ProcedureStepID { get; set; }
        //public string ProcedureID { get; set; }

        //public string ProcedureStepID { get; set; }

        public string HospitalName { get; set; } = "CIM ESPOIR Dr.LAGHOUATI";

        //public string ScheduledAET { get; set; }

    }
}
