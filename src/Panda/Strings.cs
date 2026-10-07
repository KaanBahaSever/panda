namespace Panda;

/// <summary>Chat replies in English and Turkish. {0}, {1}... are filled with string.Format.</summary>
public static class Strings
{
	static readonly Dictionary<string, (string En, string Tr)> All = new()
	{
		["searching"] = ("🔎 Looking for [i]{0}[/i]…", "🔎 [i]{0}[/i] aranıyor…"),
		["now-playing"] = ("🐼 ▶ Now playing: [b]{0}[/b] ({1})", "🐼 ▶ Şimdi çalıyor: [b]{0}[/b] ({1})"),
		["now-playing-by"] = ("🐼 ▶ Now playing: [b]{0}[/b] ({1}) · requested by {2}", "🐼 ▶ Şimdi çalıyor: [b]{0}[/b] ({1}) · isteyen: {2}"),
		["added"] = ("➕ Added to the queue (#{1}): [b]{0}[/b] ({2})", "➕ Sıraya eklendi (#{1}): [b]{0}[/b] ({2})"),
		["playing-next"] = ("⏭ Playing next: [b]{0}[/b]", "⏭ Sıradaki: [b]{0}[/b]"),
		["skipped"] = ("⏭ Skipped.", "⏭ Geçildi."),
		["stopped"] = ("⏹ Stopped and cleared the queue.", "⏹ Durdurdum ve sırayı temizledim."),
		["paused"] = ("⏸ Paused.", "⏸ Duraklatıldı."),
		["resumed"] = ("▶ Resumed.", "▶ Devam ediyor."),
		["nothing-playing"] = ("💤 Nothing is playing.", "💤 Şu an bir şey çalmıyor."),
		["queue-empty"] = ("📭 The queue is empty.", "📭 Sıra boş."),
		["queue-title"] = ("📜 Queue ({0} songs):", "📜 Sıra ({0} şarkı):"),
		["queue-more"] = ("…and {0} more", "…ve {0} şarkı daha"),
		["volume"] = ("🔊 Volume: {0}%", "🔊 Ses: %{0}"),
		["volume-set"] = ("🔊 Volume set to {0}%", "🔊 Ses %{0} yapıldı"),
		["removed"] = ("🗑 Removed: [b]{0}[/b]", "🗑 Silindi: [b]{0}[/b]"),
		["bad-index"] = ("🤔 There is no song #{0} in the queue.", "🤔 Sırada #{0} numaralı şarkı yok."),
		["cleared"] = ("🧹 Queue cleared.", "🧹 Sıra temizlendi."),
		["shuffled"] = ("🔀 Queue shuffled.", "🔀 Sıra karıştırıldı."),
		["loop-off"] = ("🔁 Loop off.", "🔁 Tekrar kapalı."),
		["loop-one"] = ("🔂 Repeating this song.", "🔂 Bu şarkı tekrar edecek."),
		["loop-all"] = ("🔁 Repeating the whole queue.", "🔁 Tüm sıra tekrar edecek."),
		["joined"] = ("🐾 Coming!", "🐾 Geliyorum!"),
		["join-failed"] = ("😿 I can't join your channel.", "😿 Kanalına giremiyorum."),
		["no-permission"] = ("🙅 You are not allowed to control me.", "🙅 Beni kontrol etme iznin yok."),
		["unknown-command"] = ("🤔 Unknown command. Try [b]{0}help[/b]", "🤔 Bilinmeyen komut. [b]{0}yardım[/b] yazabilirsin"),
		["usage-play"] = ("Usage: [b]{0}play <song name or YouTube link>[/b]", "Kullanım: [b]{0}play <şarkı adı veya YouTube linki>[/b]"),
		["queue-full"] = ("🙈 The queue is full.", "🙈 Sıra dolu."),
		["err-not-found"] = ("🔍 Nothing found on YouTube.", "🔍 YouTube'da bir şey bulamadım."),
		["err-playlist"] = ("📃 Playlists aren't supported — send a song link.", "📃 Çalma listeleri desteklenmiyor, bir şarkı linki gönder."),
		["err-not-youtube"] = ("🔗 I only play YouTube links.", "🔗 Sadece YouTube linklerini çalabiliyorum."),
		["err-too-long"] = ("⏳ That video is too long.", "⏳ Bu video çok uzun."),
		["err-bot-check"] = ("🤖 YouTube wants to check I'm not a bot. Add fresh cookies in the panel.", "🤖 YouTube bot kontrolü istiyor. Panelden yeni cookies ekleyin."),
		["err-unavailable"] = ("🚫 That video isn't available.", "🚫 Bu video kullanılamıyor."),
		["err-age"] = ("🔞 That video is age-restricted. Add cookies of an adult account in the panel.", "🔞 Bu video yaş sınırlı. Panelden yetişkin bir hesabın cookies'ini ekleyin."),
		["err-other"] = ("😿 Couldn't play that: {0}", "😿 Çalamadım: {0}"),
		["play-failed"] = ("😿 Couldn't play [b]{0}[/b]: {1}", "😿 [b]{0}[/b] çalınamadı: {1}"),
		["live"] = ("LIVE", "CANLI"),
		["help"] = (
			"🐼 [b]Panda[/b] commands:\n" +
			"[b]{0}play[/b] <song or link> – play / add to queue\n" +
			"[b]{0}skip[/b] – next song · [b]{0}stop[/b] – stop & clear\n" +
			"[b]{0}pause[/b] · [b]{0}resume[/b]\n" +
			"[b]{0}queue[/b] – show queue · [b]{0}np[/b] – now playing\n" +
			"[b]{0}vol[/b] <0-100> · [b]{0}loop[/b] <off|one|all> · [b]{0}shuffle[/b]\n" +
			"[b]{0}remove[/b] <#> · [b]{0}clear[/b] · [b]{0}join[/b] – come to my channel",
			"🐼 [b]Panda[/b] komutları:\n" +
			"[b]{0}çal[/b] <şarkı veya link> – çal / sıraya ekle\n" +
			"[b]{0}geç[/b] – sonraki şarkı · [b]{0}dur[/b] – durdur ve sırayı temizle\n" +
			"[b]{0}duraklat[/b] · [b]{0}devam[/b]\n" +
			"[b]{0}sıra[/b] – sırayı göster · [b]{0}şimdi[/b] – çalan şarkı\n" +
			"[b]{0}ses[/b] <0-100> · [b]{0}tekrar[/b] <kapalı|şarkı|hepsi> · [b]{0}karıştır[/b]\n" +
			"[b]{0}sil[/b] <#> · [b]{0}temizle[/b] · [b]{0}gel[/b] – kanalıma gel\n" +
			"(English commands like [b]{0}play[/b] work too)"),
	};

	public static string Get(string lang, string key, params object[] args)
	{
		if (!All.TryGetValue(key, out var s))
			return key;
		var text = lang == "tr" ? s.Tr : s.En;
		return args.Length == 0 ? text : string.Format(text, args);
	}

	public static string Duration(string lang, Track t) =>
		t.IsLive ? Get(lang, "live") : FormatTime(t.Duration);

	public static string FormatTime(double seconds)
	{
		var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
		return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
	}

	/// <summary>Turns a YouTubeException reason into a chat message.</summary>
	public static string Error(string lang, string reason) => reason switch
	{
		"not-found" or "empty" => Get(lang, "err-not-found"),
		"playlist" => Get(lang, "err-playlist"),
		"not-youtube" => Get(lang, "err-not-youtube"),
		"too-long" => Get(lang, "err-too-long"),
		"bot-check" => Get(lang, "err-bot-check"),
		"unavailable" => Get(lang, "err-unavailable"),
		"age" => Get(lang, "err-age"),
		_ => Get(lang, "err-other", reason),
	};

	public static string ShortError(string lang, string reason)
	{
		var msg = Error(lang, reason);
		var i = msg.IndexOf(' ');
		return i > 0 ? msg[(i + 1)..] : msg;
	}
}
