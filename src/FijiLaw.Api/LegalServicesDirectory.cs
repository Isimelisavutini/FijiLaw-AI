namespace FijiLaw.Api;

public sealed record LegalServiceLocation(
    string Id,
    string Name,
    string Type,
    string City,
    string Address,
    string? Phone,
    string? Website,
    string[] PracticeAreas,
    bool Verified,
    string VerificationNote);

public sealed class LegalServicesDirectory
{
    private static readonly LegalServiceLocation[] Locations =
    {
        new("lac-suva-criminal", "Legal Aid Commission — Suva Criminal Unit", "Legal Aid", "Suva", "41 Loftus Street, Legal Aid Building, Suva", "+679 324 1300 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"Criminal Procedure"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-suva-family", "Legal Aid Commission — Suva Family Unit", "Legal Aid", "Suva", "16 Kimberly Street, Suva", "+679 324 1301 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"Family Law","Domestic Violence"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-suva-civil", "Legal Aid Commission — Suva Civil Unit", "Legal Aid", "Suva", "73 Gordon Street, Suva", "+679 324 1302 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"Civil","Employment","Consumer Rights","Tenancy"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-nasinu", "Legal Aid Commission — Nasinu", "Legal Aid", "Nasinu", "Valelevu Complex, Nasinu", "+679 324 1304 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-nausori", "Legal Aid Commission — Nausori", "Legal Aid", "Nausori", "Lot 1 Main Street, Nausori Plaza, Level 3 Unit D03", "+679 324 1305 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-korovou", "Legal Aid Commission — Korovou", "Legal Aid", "Korovou", "Waimaro Circuit Society Building, Korovou", "+679 324 1306 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-navua", "Legal Aid Commission — Navua", "Legal Aid", "Navua", "Level 1, 4 Naitata Shankar & Son's Apartments, Navua", "+679 324 1307 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-sigatoka", "Legal Aid Commission — Sigatoka", "Legal Aid", "Sigatoka", "Level 1, Magistrate's Court Complex, Sigatoka", "+679 324 1308 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-nadi", "Legal Aid Commission — Nadi", "Legal Aid", "Nadi", "1st Floor, Units 10-13 GT Plaza, Nadi", "+679 324 1309 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-lautoka", "Legal Aid Commission — Lautoka", "Legal Aid", "Lautoka", "Level 1, Magistrate's Court Complex, Tavewa Avenue, Lautoka", "+679 324 1310 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General","Criminal Procedure","Family Law"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-lautoka-civil", "Legal Aid Commission — Lautoka Civil Unit", "Legal Aid", "Lautoka", "Level 1, Reddy Diamond Building, Marine Drive, Lautoka", "+679 324 1320 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"Civil","Employment","Consumer Rights","Tenancy"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-ba", "Legal Aid Commission — Ba", "Legal Aid", "Ba", "Magistrate's Court Complex, Old Bridge Road, Ba", "+679 324 1311 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-tavua", "Legal Aid Commission — Tavua", "Legal Aid", "Tavua", "1st Floor, Dalpat Singh Building, Nasivi Street, Tavua", "+679 324 1312 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-rakiraki", "Legal Aid Commission — Rakiraki", "Legal Aid", "Rakiraki", "1st Floor, Naidu Investments Limited Building, Main Street, Rakiraki", "+679 324 1313 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-labasa", "Legal Aid Commission — Labasa", "Legal Aid", "Labasa", "Old Court House Building, Jaduram Street, Labasa", "+679 324 1314 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-savusavu", "Legal Aid Commission — Savusavu", "Legal Aid", "Savusavu", "Level 1, Vunilagi House, Main Street, Savusavu", "+679 324 1315 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-seaqaqa", "Legal Aid Commission — Seaqaqa", "Legal Aid", "Seaqaqa", "70 Seaqaqa Township, Seaqaqa", "+679 324 1316 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-nabouwalu", "Legal Aid Commission — Nabouwalu", "Legal Aid", "Nabouwalu", "Naulumatua House, Nabouwalu", "+679 324 1317 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-taveuni", "Legal Aid Commission — Taveuni", "Legal Aid", "Taveuni", "First Light Inn Building, Ground Floor, Taveuni", "+679 324 1318 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),
        new("lac-levuka", "Legal Aid Commission — Levuka", "Legal Aid", "Levuka", "Lomaiviti Holdings Building, Beach Street, Levuka", "+679 324 1319 / 1506", "https://www.legalaid.org.fj/contact-us/", new[]{"General"}, true, "Verified from the Legal Aid Commission contact directory."),

        new("private-cromptons", "CROMPTONS Solicitors", "Private Law Firm", "Suva", "QBE Centre, Suites 10-12, 33 Victoria Parade, Suva", "+679 330 1499", null, new[]{"General"}, false, "Public business listing; practitioner/Fiji Law Society verification is pending."),
        new("private-aplegal", "AP Legal", "Private Law Firm", "Suva", "Vanuabalavu House, Corner of Ratu Sukuna Road and Crawford Avenue, Suva", "+679 330 0703", null, new[]{"General"}, false, "Public business listing; practitioner/Fiji Law Society verification is pending."),
        new("private-kslaw", "KS Law", "Private Law Firm", "Suva", "28 Disraeli Road, Suva", "+679 347 8110", null, new[]{"General","Notary"}, false, "Public business listing; practitioner/Fiji Law Society verification is pending."),
        new("private-lpb", "Lal Patel Bale Lawyers", "Private Law Firm", "Suva", "Level 8, FNPF Place, 343 Victoria Parade, Suva", "+679 331 0271", null, new[]{"Civil","Criminal Procedure","Family Law","Employment","Property"}, false, "Public business listing; practitioner/Fiji Law Society verification is pending."),
        new("private-krishna-lautoka", "Krishna & Co. Lawyers", "Private Law Firm", "Lautoka", "21 Naviti Street, Lautoka", "+679 937 0206", null, new[]{"General"}, false, "Public business listing; practitioner/Fiji Law Society verification is pending.")
    };

    public IReadOnlyList<LegalServiceLocation> Search(string? city, string? type, string? area, string? query)
    {
        IEnumerable<LegalServiceLocation> results = Locations;
        if (!string.IsNullOrWhiteSpace(city)) results = results.Where(x => x.City.Equals(city, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(type)) results = results.Where(x => x.Type.Contains(type, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(area)) results = results.Where(x => x.PracticeAreas.Any(a => a.Contains(area, StringComparison.OrdinalIgnoreCase)) || x.PracticeAreas.Contains("General"));
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            results = results.Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Address.Contains(q, StringComparison.OrdinalIgnoreCase) || x.City.Contains(q, StringComparison.OrdinalIgnoreCase) || x.PracticeAreas.Any(a => a.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }
        return results.OrderByDescending(x => x.Verified).ThenBy(x => x.City).ThenBy(x => x.Name).ToArray();
    }

    public string[] Cities() => Locations.Select(x => x.City).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
}
