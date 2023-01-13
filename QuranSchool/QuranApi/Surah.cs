using System.Collections.Generic;
using MongoDB.Entities;

namespace QuranApi;

public class Surah : Entity
{
    public int Number { get; set; }
    public string Name { get; set; }
    public string EnglishName { get; set; }
    public List<Ayah> Ayahs { get; set; }
}