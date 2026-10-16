namespace Panda;

/// <summary>Chat replies in English, Turkish and French. {0}, {1}... are filled with string.Format.</summary>
public static class Strings
{
	static readonly Dictionary<string, (string En, string Tr, string Fr)> All = new()
	{
		["searching"] = ("Looking for [i]{0}[/i]…", "[i]{0}[/i] aranıyor…", "Recherche de [i]{0}[/i]…"),
		["now-playing"] = ("🐼 Now playing: [b]{0}[/b] ({1})", "🐼 Şimdi çalıyor: [b]{0}[/b] ({1})", "🐼 En cours : [b]{0}[/b] ({1})"),
		["now-playing-by"] = ("🐼 Now playing: [b]{0}[/b] ({1}) · requested by {2}", "🐼 Şimdi çalıyor: [b]{0}[/b] ({1}) · isteyen: {2}", "🐼 En cours : [b]{0}[/b] ({1}) · demandé par {2}"),
		["added"] = ("Added to the queue (#{1}): [b]{0}[/b] ({2})", "Sıraya eklendi (#{1}): [b]{0}[/b] ({2})", "Ajouté à la file (#{1}) : [b]{0}[/b] ({2})"),
		["playing-next"] = ("Playing next: [b]{0}[/b]", "Sıradaki: [b]{0}[/b]", "À suivre : [b]{0}[/b]"),
		["skipped"] = ("Skipped.", "Geçildi.", "Morceau passé."),
		["stopped"] = ("Stopped and cleared the queue.", "Durdurdum ve sırayı temizledim.", "Lecture arrêtée et file vidée."),
		["paused"] = ("Paused.", "Duraklatıldı.", "En pause."),
		["resumed"] = ("Resumed.", "Devam ediyor.", "Lecture reprise."),
		["nothing-playing"] = ("Nothing is playing.", "Şu an bir şey çalmıyor.", "Rien n'est en cours de lecture."),
		["queue-empty"] = ("The queue is empty.", "Sıra boş.", "La file est vide."),
		["queue-title"] = ("Queue ({0} songs):", "Sıra ({0} şarkı):", "File d'attente ({0} morceaux) :"),
		["queue-more"] = ("…and {0} more", "…ve {0} şarkı daha", "…et {0} de plus"),
		["volume"] = ("Volume: {0}%", "Ses: %{0}", "Volume : {0} %"),
		["volume-set"] = ("Volume set to {0}%", "Ses %{0} yapıldı.", "Volume réglé sur {0} %"),
		["removed"] = ("Removed: [b]{0}[/b]", "Silindi: [b]{0}[/b]", "Retiré : [b]{0}[/b]"),
		["bad-index"] = ("There is no song #{0} in the queue.", "Sırada #{0} numaralı şarkı yok.", "Il n'y a pas de morceau n°{0} dans la file."),
		["cleared"] = ("Queue cleared.", "Sıra temizlendi.", "File vidée."),
		["shuffled"] = ("Queue shuffled.", "Sıra karıştırıldı.", "File mélangée."),
		["loop-off"] = ("Loop off.", "Tekrar kapalı.", "Répétition désactivée."),
		["loop-one"] = ("Repeating this song.", "Bu şarkı tekrar edecek.", "Ce morceau sera répété."),
		["loop-all"] = ("Repeating the whole queue.", "Tüm sıra tekrar edecek.", "Toute la file sera répétée."),
		["joined"] = ("🐼 Coming!", "🐼 Geliyorum!", "🐼 J'arrive !"),
		["join-failed"] = ("I can't join your channel.", "Kanalına giremiyorum.", "Je ne peux pas rejoindre ton canal."),
		["follow-on"] = ("🐾 Following you, {0}. Say [b]{1}follow[/b] again to stop.", "🐾 Seni takip ediyorum, {0}. Durdurmak için yine [b]{1}takip[/b] yaz.", "🐾 Je te suis, {0}. Écris encore [b]{1}suivre[/b] pour arrêter."),
		["follow-off"] = ("🐾 Stopped following.", "🐾 Takibi bıraktım.", "🐾 Je ne te suis plus."),
		["not-following"] = ("I'm not following anyone.", "Kimseyi takip etmiyorum.", "Je ne suis actuellement aucun utilisateur."),
		["no-permission"] = ("You are not allowed to control me.", "Beni kontrol etme iznin yok.", "Tu n'as pas le droit de me contrôler."),
		["unknown-command"] = ("Unknown command. Try [b]{0}help[/b]", "Bilinmeyen komut. [b]{0}yardım[/b] yazabilirsin.", "Commande inconnue. Essaie [b]{0}aide[/b]"),
		["usage-play"] = ("Usage: [b]{0}play <song name or YouTube link>[/b]", "Kullanım: [b]{0}çal <şarkı adı veya YouTube linki>[/b]", "Utilisation : [b]{0}jouer <nom du morceau ou lien YouTube>[/b]"),
		["queue-full"] = ("The queue is full.", "Sıra dolu.", "La file est pleine."),
		["err-not-found"] = ("Nothing found on YouTube.", "YouTube'da bir şey bulamadım.", "Rien trouvé sur YouTube."),
		["err-playlist-empty"] = ("That playlist is empty.", "Bu çalma listesi boş.", "Cette playlist est vide."),
		["err-not-youtube"] = ("I only play YouTube links.", "Sadece YouTube linklerini çalabiliyorum.", "Je ne lis que les liens YouTube."),
		["err-too-long"] = ("That video is too long.", "Bu video çok uzun.", "Cette vidéo est trop longue."),
		["err-bot-check"] = ("YouTube wants to check I'm not a bot. Add fresh cookies in the panel.", "YouTube bot kontrolü istiyor. Panelden yeni cookies ekleyin.", "YouTube veut vérifier que je ne suis pas un robot. Ajoutez de nouveaux cookies dans le panneau."),
		["err-unavailable"] = ("That video isn't available.", "Bu video kullanılamıyor.", "Cette vidéo n'est pas disponible."),
		["err-age"] = ("That video is age-restricted. Add cookies of an adult account in the panel.", "Bu video yaş sınırlı. Panelden yetişkin bir hesabın cookies'ini ekleyin.", "Cette vidéo est soumise à une limite d'âge. Ajoutez dans le panneau les cookies d'un compte adulte."),
		["err-timeout"] = ("YouTube didn't answer in time. Try again.", "YouTube zamanında cevap vermedi. Tekrar dene.", "YouTube n'a pas répondu à temps. Réessaie."),
		["err-other"] = ("Couldn't play that: {0}", "Çalamadım: {0}", "Impossible de lire ça : {0}"),
		["play-failed"] = ("Couldn't play [b]{0}[/b]: {1}", "[b]{0}[/b] çalınamadı: {1}", "Impossible de lire [b]{0}[/b] : {1}"),
		["live"] = ("LIVE", "CANLI", "EN DIRECT"),
		["radio-name"] = ("📻 Radio", "📻 Radyo", "📻 Radio"),
		["radio-on"] = ("📻 Radio on: when the queue runs out I keep playing songs like the last one.", "📻 Radyo açık: sıra bitince son şarkıya benzer şarkılar çalarım.", "📻 Radio activée : quand la file est finie, je continue avec des morceaux proches du dernier."),
		["radio-off"] = ("📻 Radio off.", "📻 Radyo kapalı.", "📻 Radio désactivée."),
		["playlist-added"] = ("📃 Added {0} songs from [b]{1}[/b].", "📃 [b]{1}[/b] listesinden {0} şarkı sıraya eklendi.", "📃 {0} morceaux de [b]{1}[/b] ajoutés à la file."),
		["playlist-not-found"] = ("There is no playlist called [b]{0}[/b]. {1}", "[b]{0}[/b] adında bir liste yok. {1}", "Il n'y a pas de playlist nommée [b]{0}[/b]. {1}"),
		["playlists"] = ("📃 Playlists: {0}", "📃 Listeler: {0}", "📃 Playlists : {0}"),
		["playlists-none"] = ("No saved playlists yet. Make one in the panel.", "Henüz kayıtlı liste yok. Panelden oluşturabilirsin.", "Aucune playlist enregistrée pour l'instant. Crée-en une dans le panneau."),
		["favorites"] = ("Favorites", "Favoriler", "Favoris"),
		["playlist-full"] = ("[b]{0}[/b] is full ({1} songs). Remove some in the panel first.", "[b]{0}[/b] dolu ({1} şarkı). Önce panelden birkaçını sil.", "[b]{0}[/b] est pleine ({1} morceaux). Retire d'abord quelques morceaux dans le panneau."),
		["fav-added"] = ("⭐ Saved to [b]{1}[/b]: {0}", "⭐ [b]{1}[/b] listesine eklendi: {0}", "⭐ Ajouté à [b]{1}[/b] : {0}"),
		["help"] = (
			"🐼 [b]Panda[/b] commands:\n" +
			"[b]{0}play[/b] <song or link> – play / add to queue · [b]{0}playnow[/b] <song> – play right away\n" +
			"[b]{0}skip[/b] – next song · [b]{0}stop[/b] – stop & clear\n" +
			"[b]{0}pause[/b] · [b]{0}resume[/b]\n" +
			"[b]{0}queue[/b] – show queue · [b]{0}np[/b] – now playing\n" +
			"[b]{0}vol[/b] <0-100> · [b]{0}loop[/b] <off|one|all> · [b]{0}shuffle[/b]\n" +
			"[b]{0}remove[/b] <#> · [b]{0}clear[/b] · [b]{0}join[/b] – come to my channel · [b]{0}follow[/b] – follow me around\n" +
			"[b]{0}radio[/b] <on|off> – keep playing similar songs when the queue ends\n" +
			"[b]{0}playlist[/b] <name> – queue a saved playlist · [b]{0}playlists[/b] · [b]{0}fav[/b] – save this song to Favorites",
			"🐼 [b]Panda[/b] komutları:\n" +
			"[b]{0}çal[/b] <şarkı veya link> – çal / sıraya ekle · [b]{0}şimdiçal[/b] <şarkı> – hemen çal\n" +
			"[b]{0}geç[/b] – sonraki şarkı · [b]{0}dur[/b] – durdur ve sırayı temizle\n" +
			"[b]{0}duraklat[/b] · [b]{0}devam[/b]\n" +
			"[b]{0}sıra[/b] – sırayı göster · [b]{0}şimdi[/b] – çalan şarkı\n" +
			"[b]{0}ses[/b] <0-100> · [b]{0}tekrar[/b] <kapalı|şarkı|hepsi> · [b]{0}karıştır[/b]\n" +
			"[b]{0}sil[/b] <#> · [b]{0}temizle[/b] · [b]{0}gel[/b] – kanalıma gel · [b]{0}takip[/b] – beni takip et\n" +
			"[b]{0}radyo[/b] <aç|kapat> – sıra bitince benzer şarkılar çalmaya devam et\n" +
			"[b]{0}liste[/b] <ad> – kayıtlı listeyi sıraya ekle · [b]{0}listeler[/b] · [b]{0}fav[/b] – bu şarkıyı Favoriler'e kaydet\n" +
			"(English commands like [b]{0}play[/b] work too)",
			"🐼 Commandes de [b]Panda[/b] :\n" +
			"[b]{0}jouer[/b] <morceau ou lien> – lire / ajouter à la file · [b]{0}jouermaintenant[/b] <morceau> – lire tout de suite\n" +
			"[b]{0}passer[/b] – morceau suivant · [b]{0}arrêt[/b] – arrêter et vider la file\n" +
			"[b]{0}pause[/b] · [b]{0}reprendre[/b]\n" +
			"[b]{0}file[/b] – voir la file · [b]{0}encours[/b] – morceau en cours\n" +
			"[b]{0}volume[/b] <0-100> · [b]{0}boucle[/b] <non|un|tout> · [b]{0}mélanger[/b]\n" +
			"[b]{0}retirer[/b] <#> · [b]{0}vider[/b] · [b]{0}viens[/b] – viens dans mon canal · [b]{0}suivre[/b] – suis-moi de canal en canal\n" +
			"[b]{0}radio[/b] <oui|non> – continuer avec des morceaux proches quand la file est finie\n" +
			"[b]{0}playlist[/b] <nom> – ajouter une playlist enregistrée · [b]{0}playlists[/b] · [b]{0}fav[/b] – enregistrer ce morceau dans les Favoris\n" +
			"(les commandes anglaises comme [b]{0}play[/b] marchent aussi)"),
	};

	/// <summary>Chat languages, in the order the panel offers them.</summary>
	public static readonly string[] Languages = ["en", "tr", "fr"];

	public static string Get(string lang, string key, params object[] args)
	{
		if (!All.TryGetValue(key, out var s))
			return key;
		var text = lang switch { "tr" => s.Tr, "fr" => s.Fr, _ => s.En };
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
		"playlist-empty" => Get(lang, "err-playlist-empty"),
		"not-youtube" => Get(lang, "err-not-youtube"),
		"too-long" => Get(lang, "err-too-long"),
		"bot-check" => Get(lang, "err-bot-check"),
		"unavailable" => Get(lang, "err-unavailable"),
		"age" => Get(lang, "err-age"),
		"timeout" => Get(lang, "err-timeout"),
		_ => Get(lang, "err-other", reason),
	};

	/// <summary>The reason without the "Couldn't play that:" wrapper, for "Couldn't play X: reason".</summary>
	public static string ShortError(string lang, string reason) =>
		reason is "not-found" or "empty" or "playlist-empty" or "not-youtube" or "too-long" or "bot-check" or "unavailable" or "age" or "timeout"
			? Error(lang, reason)
			: reason;
}
