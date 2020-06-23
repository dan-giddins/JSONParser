using AutoMapper;
using Newtonsoft.Json;
using Ris.Worktribe.DTOs.Ingest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UoN.Api.Staff.Library.DTO;

namespace JSONParser
{
	class Program
	{
		private const string ReadDir =
			@"C:\__RIS_Data\worktribe\";
		private const string Filename = "2020_06_10_16_59_42_worktribe_Development_Archiving";
		private const string NewListFilename = "2020_06_10_16_59_42_worktribe_Development_Archiving";
		private const string OldListFilename = "2019_11_22_16_09_58_worktribe_Live_Archiving";
		private const bool WriteMergedFile = true;

		static void Main()
		{
			//ComparePeople();
			//CountStaff($@"{ReadDir}hr\2020_02_25_15_42_46_hr_Development.json");
			FormatList();
		}

		private static void FormatList()
		{
			foreach (var person in GetPeople<PersonIngest>(Filename))
			{
				Console.WriteLine($"{person.Id} - {person.FirstName} {person.Surname}");
			}
		}

		private static void CountStaff(string filename)
		{
			Func<Staff, bool> filter = null;
			// offscale
			var people = GetPeople<Staff>(filename);
			var matched = people.Where(filter);
		}

		private static void ComparePeople()
		{
			var oldList = GetPeople<PersonIngest>(OldListFilename);
			var newList = GetPeople<PersonIngest>(NewListFilename);
			var mergedList = new List<PersonIngest>();
			// look though all oldPeople
			foreach (var oldPerson in oldList)
			{
				// look for new people that match the id
				var newPeople = newList.Where(x => x.Id == oldPerson.Id);
				if (newPeople.Count() == 0)
				{
					Console.WriteLine($"Person {oldPerson.Id} is only in the old list");
				}
				else if (newPeople.Count() > 1)
				{
					Console.WriteLine($"Person {oldPerson.Id} is in the new list multiple times");
					var oldPeople = oldList.Where(x => x.Id == oldPerson.Id);
					if (oldPeople.Count() > 1)
					{
						Console.WriteLine($"Person {oldPerson.Id} is also in the old list multiple times");
					}
				}
				else
				{
					var newPerson = newPeople.First();
					// clone the newPerson
					var mergedPerson = CloneObject(newPerson);
					mergedPerson.Appointments = new List<AppointmentIngest>();
					// check appointments
					foreach (var oldAppointment in oldPerson.Appointments)
					{
						// convert the new ID format back to the old (just incase)
						var idSegments = oldAppointment.PostId.Split('_');
						oldAppointment.PostId = $"{idSegments[0]}_{idSegments[1]}";
						// add archived old appointment to merge list if its currently unarchived
						if (oldAppointment.Archive == "No")
						{
							// clone appointment
							var mergeAppointment = CloneObject(oldAppointment);
							// archive
							mergeAppointment.Archive = "Yes";
							mergedPerson.Appointments.Add(mergeAppointment);
						}
						// look for new appointments that match the id
						var newAppointments = newPerson.Appointments
							.Where(x => x.PostId[..x.PostId.LastIndexOf('_')] == oldAppointment.PostId);
						if (newAppointments.Count() == 0)
						{
							Console.WriteLine($"\nAppointment {oldAppointment.PostId} ({oldAppointment.Fte}) is only in the old list");
						}
						else
						{
							if (newAppointments.Count() > 1)
							{
								Console.WriteLine($"\nAppointment {oldAppointment.PostId} ({oldAppointment.Fte}) ({oldAppointment.StartDate}) is in the new list multiple times:");
								foreach (var newAppointment in newAppointments)
								{
									Console.WriteLine($"{newAppointment.PostId} {newAppointment.Fte}");
								}
								var oldAppointments = oldList.Where(x => x.Id == oldAppointment.PostId);
								if (oldAppointments.Count() > 1)
								{
									Console.WriteLine($"\nAppointment {oldAppointment.PostId} is also in the old list multiple times!");
								}
							}
						}
					}
					// look for new appointments not in old list
					foreach (var newAppointment in newPerson.Appointments)
					{
						if (!oldPerson.Appointments.Any(x =>
							x.PostId == newAppointment.PostId[..newAppointment.PostId.LastIndexOf('_')]))
						{
							Console.WriteLine($"\nAppointment {newAppointment.PostId} ({newAppointment.Fte}) is only in the new list");
						}
					}
					// add new unarchived appointments to merge list
					mergedPerson.Appointments.AddRange(newPerson.Appointments.Where(x => x.Archive == "No"));
					mergedList.Add(mergedPerson);
				}
			}
			// create merge file
			if (WriteMergedFile)
			{
				var dateTimeString = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
				var filepath = $"{ReadDir}\\{dateTimeString}_worktribe_Merged.json";
				File.WriteAllText(filepath, JsonConvert.SerializeObject(mergedList, Formatting.Indented));
			}
			// look for new people not in old list
			foreach (var newPerson in newList)
			{
				if (!oldList.Any(x => x.Id == newPerson.Id))
				{
					Console.WriteLine($"Person {newPerson.Id} is only in the new list");
				}
			}
		}

		private static T CloneObject<T>(T o) =>
			new MapperConfiguration(x => x.CreateMap<T, T>()).CreateMapper().Map<T>(o);

		private static IList<T> GetPeople<T>(string filename) =>
			JsonConvert.DeserializeObject<IList<T>>(
				File.ReadAllText($"{ReadDir + filename}.json"));
	}
}
