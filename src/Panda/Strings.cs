namespace Panda;

/// <summary>Chat replies in English, Turkish, French and German. {0}, {1}... are filled with string.Format.</summary>
public static class Strings
{
	static readonly Dictionary<string, (string En, string Tr, string Fr, string De)> All = new()
	{
		["searching"] = ("Looking for [i]{0}[/i]…", "[i]{0}[/i] aranıyor…", "Recherche de [i]{0}[/i]…", "Suche nach [i]{0}[/i]…"),
		["now-playing"] = ("🐼 Now playing: [b]{0}[/b] ({1})", "🐼 Şimdi çalıyor: [b]{0}[/b] ({1})", "🐼 En cours : [b]{0}[/b] ({1})", "🐼 Läuft gerade: [b]{0}[/b] ({1})"),
		["now-playing-by"] = ("🐼 Now playing: [b]{0}[/b] ({1}) · requested by {2}", "🐼 Şimdi çalıyor: [b]{0}[/b] ({1}) · isteyen: {2}", "🐼 En cours : [b]{0}[/b] ({1}) · demandé par {2}", "🐼 Läuft gerade: [b]{0}[/b] ({1}) · gewünscht von {2}"),
		["added"] = ("Added to the queue (#{1}): [b]{0}[/b] ({2})", "Sıraya eklendi (#{1}): [b]{0}[/b] ({2})", "Ajouté à la file (#{1}) : [b]{0}[/b] ({2})", "Zur Warteschlange hinzugefügt (#{1}): [b]{0}[/b] ({2})"),
		["playing-next"] = ("Playing next: [b]{0}[/b]", "Sıradaki: [b]{0}[/b]", "À suivre : [b]{0}[/b]", "Als Nächstes: [b]{0}[/b]"),
		["vote-progress"] = ("🗳 Vote to skip: {0}/{1}.", "🗳 Geçme oyu: {0}/{1}.", "🗳 Vote pour passer : {0}/{1}.", "🗳 Abstimmung zum Überspringen: {0}/{1}."),
		["vote-skipped"] = ("🗳 Skipped by vote ({0}/{1}).", "🗳 Oylamayla geçildi ({0}/{1}).", "🗳 Passé par vote ({0}/{1}).", "🗳 Per Abstimmung übersprungen ({0}/{1})."),
		["vote-not-here"] = ("Only people in my channel can vote.", "Sadece benim kanalımdakiler oy verebilir.", "Seules les personnes dans mon canal peuvent voter.", "Nur Leute in meinem Channel können abstimmen."),
		["usage-search"] = ("Usage: [b]{0}search <song name>[/b]", "Kullanım: [b]{0}ara <şarkı adı>[/b]", "Utilisation : [b]{0}chercher <nom du morceau>[/b]", "Verwendung: [b]{0}suchen <Songname>[/b]"),
		["search-results"] = ("🔎 Pick one with [b]{0}1[/b] … [b]{0}{1}[/b]:", "🔎 [b]{0}1[/b] … [b]{0}{1}[/b] ile seç:", "🔎 Choisis avec [b]{0}1[/b] … [b]{0}{1}[/b] :", "🔎 Wähl einen mit [b]{0}1[/b] … [b]{0}{1}[/b]:"),
		["pick-first"] = ("Search first: [b]{0}search <song name>[/b]", "Önce ara: [b]{0}ara <şarkı adı>[/b]", "Cherche d'abord : [b]{0}chercher <nom du morceau>[/b]", "Such zuerst: [b]{0}suchen <Songname>[/b]"),
		["pick-bad"] = ("There is no result #{0}.", "#{0} numaralı bir sonuç yok.", "Il n'y a pas de résultat n°{0}.", "Es gibt kein Ergebnis #{0}."),
		["usage-seek"] = ("Usage: [b]{0}seek 1:30[/b] (or [b]+10[/b] / [b]-10[/b] seconds)", "Kullanım: [b]{0}sar 1:30[/b] (ya da [b]+10[/b] / [b]-10[/b] saniye)", "Utilisation : [b]{0}aller 1:30[/b] (ou [b]+10[/b] / [b]-10[/b] secondes)", "Verwendung: [b]{0}spulen 1:30[/b] (oder [b]+10[/b] / [b]-10[/b] Sekunden)"),
		["seeked"] = ("⏩ Jumped to {0}.", "⏩ {0} noktasına gidildi.", "⏩ Reprise à {0}.", "⏩ Zu {0} gesprungen."),
		["cant-seek"] = ("You can't seek in a live stream.", "Canlı yayında ileri geri sarılamaz.", "On ne peut pas se déplacer dans un direct.", "In einem Livestream kann man nicht spulen."),
		["back"] = ("⏮ Playing the previous song again: [b]{0}[/b]", "⏮ Önceki şarkı tekrar çalıyor: [b]{0}[/b]", "⏮ Retour au morceau précédent : [b]{0}[/b]", "⏮ Der vorige Song läuft nochmal: [b]{0}[/b]"),
		["back-none"] = ("Nothing was played before this.", "Bundan önce bir şey çalınmadı.", "Rien n'a été joué avant.", "Davor wurde nichts gespielt."),
		["usage-move"] = ("Usage: [b]{0}move <from #> <to #>[/b]", "Kullanım: [b]{0}taşı <nereden #> <nereye #>[/b]", "Utilisation : [b]{0}déplacer <de #> <à #>[/b]", "Verwendung: [b]{0}verschieben <von #> <nach #>[/b]"),
		["moved"] = ("Moved [b]{0}[/b] to #{1}.", "[b]{0}[/b] #{1} sırasına taşındı.", "[b]{0}[/b] déplacé en n°{1}.", "[b]{0}[/b] auf #{1} verschoben."),
		["skipped"] = ("Skipped.", "Geçildi.", "Morceau passé.", "Übersprungen."),
		["stopped"] = ("Stopped and cleared the queue.", "Durdurdum ve sırayı temizledim.", "Lecture arrêtée et file vidée.", "Gestoppt und die Warteschlange geleert."),
		["paused"] = ("Paused.", "Duraklatıldı.", "En pause.", "Pausiert."),
		["resumed"] = ("Resumed.", "Devam ediyor.", "Lecture reprise.", "Geht weiter."),
		["nothing-playing"] = ("Nothing is playing.", "Şu an bir şey çalmıyor.", "Rien n'est en cours de lecture.", "Gerade läuft nichts."),
		["queue-empty"] = ("The queue is empty.", "Sıra boş.", "La file est vide.", "Die Warteschlange ist leer."),
		["queue-title"] = ("Queue ({0} songs):", "Sıra ({0} şarkı):", "File d'attente ({0} morceaux) :", "Warteschlange ({0} Songs):"),
		["queue-more"] = ("…and {0} more", "…ve {0} şarkı daha", "…et {0} de plus", "…und {0} weitere"),
		["volume"] = ("Volume: {0}%", "Ses: %{0}", "Volume : {0} %", "Lautstärke: {0} %"),
		["volume-set"] = ("Volume set to {0}%", "Ses %{0} yapıldı.", "Volume réglé sur {0} %", "Lautstärke auf {0} % gesetzt."),
		["removed"] = ("Removed: [b]{0}[/b]", "Silindi: [b]{0}[/b]", "Retiré : [b]{0}[/b]", "Entfernt: [b]{0}[/b]"),
		["bad-index"] = ("There is no song #{0} in the queue.", "Sırada #{0} numaralı şarkı yok.", "Il n'y a pas de morceau n°{0} dans la file.", "In der Warteschlange gibt es keinen Song #{0}."),
		["cleared"] = ("Queue cleared.", "Sıra temizlendi.", "File vidée.", "Warteschlange geleert."),
		["shuffled"] = ("Queue shuffled.", "Sıra karıştırıldı.", "File mélangée.", "Warteschlange gemischt."),
		["loop-off"] = ("Loop off.", "Tekrar kapalı.", "Répétition désactivée.", "Wiederholung aus."),
		["loop-one"] = ("Repeating this song.", "Bu şarkı tekrar edecek.", "Ce morceau sera répété.", "Dieser Song wird wiederholt."),
		["loop-all"] = ("Repeating the whole queue.", "Tüm sıra tekrar edecek.", "Toute la file sera répétée.", "Die ganze Warteschlange wird wiederholt."),
		["joined"] = ("🐼 Coming!", "🐼 Geliyorum!", "🐼 J'arrive !", "🐼 Bin unterwegs!"),
		["join-failed"] = ("I can't join your channel.", "Kanalına giremiyorum.", "Je ne peux pas rejoindre ton canal.", "Ich kann deinen Channel nicht betreten."),
		["follow-on"] = ("🐾 Following you, {0}. Say [b]{1}follow[/b] again to stop.", "🐾 Seni takip ediyorum, {0}. Durdurmak için yine [b]{1}takip[/b] yaz.", "🐾 Je te suis, {0}. Écris encore [b]{1}suivre[/b] pour arrêter.", "🐾 Ich folge dir, {0}. Schreib nochmal [b]{1}folgen[/b], um aufzuhören."),
		["follow-off"] = ("🐾 Stopped following.", "🐾 Takibi bıraktım.", "🐾 Je ne te suis plus.", "🐾 Ich folge niemandem mehr."),
		["not-following"] = ("I'm not following anyone.", "Kimseyi takip etmiyorum.", "Je ne suis actuellement aucun utilisateur.", "Ich folge gerade niemandem."),
		["need-level"] = ("[b]{0}[/b] is only for {1}.", "[b]{0}[/b] sadece {1} için.", "[b]{0}[/b] est réservé aux {1}.", "[b]{0}[/b] ist nur für {1}."),
		["level-dj"] = ("DJs and admins", "DJ'ler ve yöneticiler", "DJ et admins", "DJs und Admins"),
		["level-admin"] = ("admins", "yöneticiler", "admins", "Admins"),
		["others-song"] = ("Only {0} can change other people's songs.", "Başkalarının şarkılarını sadece {0} değiştirebilir.",
			"Seuls les {0} peuvent toucher aux morceaux des autres.", "Nur {0} dürfen die Songs anderer ändern."),
		["only-in-channel"] = ("Come to my channel to use me.", "Beni kullanmak için kanalıma gel.", "Viens dans mon canal pour m'utiliser.",
			"Komm in meinen Channel, um mich zu benutzen."),
		["no-permission"] = ("You are not allowed to control me.", "Beni kontrol etme iznin yok.", "Tu n'as pas le droit de me contrôler.", "Du darfst mich nicht steuern."),
		["unknown-command"] = ("Unknown command. Try [b]{0}help[/b]", "Bilinmeyen komut. [b]{0}yardım[/b] yazabilirsin.", "Commande inconnue. Essaie [b]{0}aide[/b]", "Unbekannter Befehl. Versuch's mit [b]{0}hilfe[/b]"),
		["usage-play"] = ("Usage: [b]{0}play <song name or YouTube link>[/b]", "Kullanım: [b]{0}çal <şarkı adı veya YouTube linki>[/b]", "Utilisation : [b]{0}jouer <nom du morceau ou lien YouTube>[/b]", "Verwendung: [b]{0}spielen <Songname oder YouTube-Link>[/b]"),
		["user-limit"] = ("You already have as many songs waiting as allowed ({0}). Add more when one of them has played.",
			"Sırada izin verilen sayıda ({0}) bekleyen şarkın var. Biri çalınca yenisini ekleyebilirsin.",
			"Tu as déjà le nombre maximum de morceaux en attente ({0}). Ajoute-en d'autres quand l'un d'eux aura été joué.",
			"Du hast schon die erlaubte Anzahl Songs ({0}) in der Warteschlange. Füg neue hinzu, wenn einer davon gespielt wurde."),
		["vote-needed"] = ("This song is someone else's: vote with [b]{0}voteskip[/b].", "Bu şarkıyı başkası istedi: [b]{0}oyla[/b] ile oy ver.",
			"Ce morceau est celui de quelqu'un d'autre : vote avec [b]{0}voter[/b].", "Diesen Song hat jemand anderes gewünscht: stimm mit [b]{0}abstimmen[/b] ab."),
		["queue-full"] = ("The queue is full.", "Sıra dolu.", "La file est pleine.", "Die Warteschlange ist voll."),
		["err-not-found"] = ("Nothing found.", "Bir şey bulamadım.", "Rien trouvé.", "Nichts gefunden."),
		["err-source-off"] = ("That source is turned off in the panel.", "Bu kaynak panelde kapalı.", "Cette source est désactivée dans le panneau.", "Diese Quelle ist im Panel ausgeschaltet."),
		["err-link-private"] = ("I don't open links into the local network.", "Yerel ağdaki linkleri açmıyorum.", "Je n'ouvre pas les liens vers le réseau local.", "Ich öffne keine Links ins lokale Netzwerk."),
		["err-link-unreachable"] = ("I couldn't open that link.", "Bu linki açamadım.", "Je n'ai pas pu ouvrir ce lien.", "Ich konnte den Link nicht öffnen."),
		["err-link-unplayable"] = ("I found nothing to play at that link.", "O linkte çalacak bir şey bulamadım.", "Je n'ai rien trouvé à lire à ce lien.", "Unter dem Link habe ich nichts zum Abspielen gefunden."),
		["err-playlist-empty"] = ("That playlist is empty.", "Bu çalma listesi boş.", "Cette playlist est vide.", "Diese Playlist ist leer."),
		["err-not-youtube"] = ("I only play YouTube links.", "Sadece YouTube linklerini çalabiliyorum.", "Je ne lis que les liens YouTube.", "Ich spiele nur YouTube-Links."),
		["err-too-long"] = ("That video is too long.", "Bu video çok uzun.", "Cette vidéo est trop longue.", "Das Video ist zu lang."),
		["err-bot-check"] = ("YouTube wants to check I'm not a bot. Add fresh cookies in the panel.", "YouTube bot kontrolü istiyor. Panelden yeni cookies ekleyin.", "YouTube veut vérifier que je ne suis pas un robot. Ajoutez de nouveaux cookies dans le panneau.", "YouTube will sichergehen, dass ich kein Bot bin. Füge im Panel neue Cookies hinzu."),
		["err-unavailable"] = ("That video isn't available.", "Bu video kullanılamıyor.", "Cette vidéo n'est pas disponible.", "Dieses Video ist nicht verfügbar."),
		["err-age"] = ("That video is age-restricted. Add cookies of an adult account in the panel.", "Bu video yaş sınırlı. Panelden yetişkin bir hesabın cookies'ini ekleyin.", "Cette vidéo est soumise à une limite d'âge. Ajoutez dans le panneau les cookies d'un compte adulte.", "Dieses Video hat eine Altersbeschränkung. Füge im Panel die Cookies eines volljährigen Kontos hinzu."),
		["err-timeout"] = ("No answer in time. Try again.", "Zamanında cevap gelmedi. Tekrar dene.", "Pas de réponse à temps. Réessaie.", "Keine rechtzeitige Antwort. Versuch es nochmal."),
		["err-other"] = ("Couldn't play that: {0}", "Çalamadım: {0}", "Impossible de lire ça : {0}", "Konnte das nicht abspielen: {0}"),
		["play-failed"] = ("Couldn't play [b]{0}[/b]: {1}", "[b]{0}[/b] çalınamadı: {1}", "Impossible de lire [b]{0}[/b] : {1}", "Konnte [b]{0}[/b] nicht abspielen: {1}"),
		["live"] = ("LIVE", "CANLI", "EN DIRECT", "LIVE"),
		["autoplay-name"] = ("✨ Autoplay", "✨ Otomatik devam", "✨ Lecture auto", "✨ Autoplay"),
		["autoplay-on"] = ("✨ Autoplay on: when the queue runs out I keep playing songs like the last one.", "✨ Otomatik devam açık: sıra bitince son şarkıya benzer şarkılar çalarım.", "✨ Lecture auto activée : quand la file est finie, je continue avec des morceaux proches du dernier.", "✨ Autoplay an: Wenn die Warteschlange leer ist, spiele ich Songs, die dem letzten ähneln."),
		["autoplay-off"] = ("✨ Autoplay off.", "✨ Otomatik devam kapalı.", "✨ Lecture auto désactivée.", "✨ Autoplay aus."),
		["radio-renamed"] = ("(This is now [b]{0}autoplay[/b]; [b]{0}fm <name>[/b] plays an internet radio station.)", "(Bunun adı artık [b]{0}otomatik[/b]; [b]{0}fm <ad>[/b] bir internet radyosu çalar.)", "(C'est maintenant [b]{0}auto[/b] ; [b]{0}fm <nom>[/b] lit une radio sur Internet.)", "(Das heißt jetzt [b]{0}autoplay[/b]; [b]{0}fm <Name>[/b] spielt einen Internetradiosender.)"),
		["fm-list"] = ("📻 Favorite stations (play one with [b]{0}fm <number or name>[/b]):", "📻 Favori radyolar ([b]{0}fm <numara ya da ad>[/b] ile çal):", "📻 Radios favorites (lance-en une avec [b]{0}fm <numéro ou nom>[/b]) :", "📻 Lieblingssender (spiel einen mit [b]{0}fm <Nummer oder Name>[/b]):"),
		["fm-none"] = ("📻 No favorite stations yet. [b]{0}fm <name>[/b] finds one on Radio Browser; [b]{0}fav[/b] while it plays keeps it.", "📻 Henüz favori radyo yok. [b]{0}fm <ad>[/b] Radio Browser'da arar; çalarken [b]{0}fav[/b] favorilere ekler.", "📻 Pas encore de radios favorites. [b]{0}fm <nom>[/b] en trouve une sur Radio Browser ; [b]{0}fav[/b] pendant qu'elle joue la garde.", "📻 Noch keine Lieblingssender. [b]{0}fm <Name>[/b] sucht einen bei Radio Browser; [b]{0}fav[/b], während er läuft, behält ihn."),
		["fm-not-found"] = ("No radio station called [b]{0}[/b] found.", "[b]{0}[/b] adında bir radyo bulamadım.", "Aucune radio nommée [b]{0}[/b] trouvée.", "Kein Radiosender namens [b]{0}[/b] gefunden."),
		["fm-down"] = ("The radio list (Radio Browser) doesn't answer right now. Try again later.", "Radyo listesi (Radio Browser) şu an cevap vermiyor. Sonra tekrar dene.", "La liste des radios (Radio Browser) ne répond pas pour l'instant. Réessaie plus tard.", "Die Senderliste (Radio Browser) antwortet gerade nicht. Versuch es später nochmal."),
		["fm-fav-added"] = ("📻 [b]{0}[/b] is now a favorite station ([b]{1}fm[/b] lists them).", "📻 [b]{0}[/b] favori radyolara eklendi ([b]{1}fm[/b] listeler).", "📻 [b]{0}[/b] fait maintenant partie des radios favorites ([b]{1}fm[/b] les liste).", "📻 [b]{0}[/b] ist jetzt ein Lieblingssender ([b]{1}fm[/b] zeigt sie)."),
		["fm-fav-full"] = ("There are already {0} favorite stations. Remove some in the panel first.", "Zaten {0} favori radyo var. Önce panelden birkaçını sil.", "Il y a déjà {0} radios favorites. Retire-en d'abord dans le panneau.", "Es gibt schon {0} Lieblingssender. Entferne zuerst ein paar im Panel."),
		["list-adding"] = ("📃 {0} more songs from [b]{1}[/b] are looked up and added one by one.", "📃 [b]{1}[/b] listesindeki diğer {0} şarkı aranıp sırayla ekleniyor.", "📃 Les {0} autres morceaux de [b]{1}[/b] sont cherchés et ajoutés un par un.", "📃 Die {0} weiteren Songs aus [b]{1}[/b] werden gesucht und nach und nach hinzugefügt."),
		["list-added"] = ("📃 Done: {0} songs from [b]{1}[/b] are in the queue.", "📃 Bitti: [b]{1}[/b] listesinden {0} şarkı sırada.", "📃 Terminé : {0} morceaux de [b]{1}[/b] sont dans la file.", "📃 Fertig: {0} Songs aus [b]{1}[/b] sind in der Warteschlange."),
		["myfav-added"] = ("⭐ Saved to your own list ({1} songs): {0}", "⭐ Kendi listene kaydedildi ({1} şarkı): {0}", "⭐ Enregistré dans ta liste ({1} morceaux) : {0}", "⭐ In deiner eigenen Liste gespeichert ({1} Songs): {0}"),
		["myfav-empty"] = ("Your own list is empty. While a song plays, [b]{0}myfav[/b] saves it.", "Kendi listen boş. Bir şarkı çalarken [b]{0}myfav[/b] onu kaydeder.", "Ta liste est vide. Pendant qu'un morceau joue, [b]{0}myfav[/b] l'enregistre.", "Deine eigene Liste ist leer. Während ein Song läuft, speichert [b]{0}myfav[/b] ihn."),
		["myfav-list"] = ("⭐ Your own list ({0} songs; [b]{1}myfav play[/b], [b]{1}myfav remove <#>[/b]):", "⭐ Kendi listen ({0} şarkı; [b]{1}myfav çal[/b], [b]{1}myfav sil <#>[/b]):", "⭐ Ta liste ({0} morceaux ; [b]{1}myfav jouer[/b], [b]{1}myfav retirer <#>[/b]) :", "⭐ Deine eigene Liste ({0} Songs; [b]{1}myfav spielen[/b], [b]{1}myfav entfernen <#>[/b]):"),
		["myfav-removed"] = ("⭐ Removed from your list: [b]{0}[/b]", "⭐ Listenden çıkarıldı: [b]{0}[/b]", "⭐ Retiré de ta liste : [b]{0}[/b]", "⭐ Aus deiner Liste entfernt: [b]{0}[/b]"),
		["myfav-usage"] = ("[b]{0}myfav[/b] saves the song playing to your own list; [b]{0}myfav play[/b] (or [b]shuffle[/b]) queues it, [b]{0}myfav list[/b] shows it, [b]{0}myfav remove <#>[/b].", "[b]{0}myfav[/b] çalan şarkıyı kendi listene kaydeder; [b]{0}myfav çal[/b] (ya da [b]karıştır[/b]) sıraya ekler, [b]{0}myfav liste[/b] gösterir, [b]{0}myfav sil <#>[/b].", "[b]{0}myfav[/b] enregistre le morceau en cours dans ta liste ; [b]{0}myfav jouer[/b] (ou [b]mélanger[/b]) l'ajoute à la file, [b]{0}myfav liste[/b] la montre, [b]{0}myfav retirer <#>[/b].", "[b]{0}myfav[/b] speichert den laufenden Song in deiner Liste; [b]{0}myfav spielen[/b] (oder [b]mischen[/b]) reiht sie ein, [b]{0}myfav liste[/b] zeigt sie, [b]{0}myfav entfernen <#>[/b]."),
		["myfav-live"] = ("A live stream can't go in a list; a station goes to the favorite stations with [b]{0}fav[/b].", "Canlı yayın listeye eklenemez; radyoyu [b]{0}fav[/b] favori radyolara ekler.", "Un direct ne peut pas aller dans une liste ; une radio va dans les radios favorites avec [b]{0}fav[/b].", "Ein Livestream kann nicht in eine Liste; ein Sender kommt mit [b]{0}fav[/b] zu den Lieblingssendern."),
		["myfav-no-id"] = ("I can't tell who you are (no unique id).", "Kim olduğunu anlayamadım (benzersiz kimlik yok).", "Je ne sais pas qui tu es (pas d'identifiant unique).", "Ich weiß nicht, wer du bist (keine Unique-ID)."),
		["eq-now"] = ("🎛️ Equalizer: [b]{0}[/b]. Choose one with [b]{2}eq <name>[/b]: {1}.", "🎛️ Ekolayzer: [b]{0}[/b]. [b]{2}eq <ad>[/b] ile seç: {1}.", "🎛️ Égaliseur : [b]{0}[/b]. Choisis avec [b]{2}eq <nom>[/b] : {1}.", "🎛️ Equalizer: [b]{0}[/b]. Wähl einen mit [b]{2}eq <Name>[/b]: {1}."),
		["eq-set"] = ("🎛️ Equalizer: [b]{0}[/b].", "🎛️ Ekolayzer: [b]{0}[/b].", "🎛️ Égaliseur : [b]{0}[/b].", "🎛️ Equalizer: [b]{0}[/b]."),
		["eq-off"] = ("🎛️ Equalizer off.", "🎛️ Ekolayzer kapalı.", "🎛️ Égaliseur désactivé.", "🎛️ Equalizer aus."),
		["eq-unknown"] = ("There is no equalizer called [b]{0}[/b]. Try: {1}.", "[b]{0}[/b] adında bir ekolayzer ayarı yok. Şunlardan biri: {1}.", "Il n'y a pas d'égaliseur nommé [b]{0}[/b]. Essaie : {1}.", "Es gibt keinen Equalizer namens [b]{0}[/b]. Versuch: {1}."),
		["eq-custom-empty"] = ("Set your own bands in the panel first (🎛️ under the player).", "Önce kendi bantlarını panelden ayarla (oynatıcının altındaki 🎛️).", "Règle d'abord tes propres bandes dans le panneau (🎛️ sous le lecteur).", "Stell zuerst deine eigenen Bänder im Panel ein (🎛️ unter dem Player)."),
		["eqn-off"] = ("off", "kapalı", "désactivé", "aus"),
		["eqn-bass"] = ("bass", "bas", "basses", "Bass"),
		["eqn-treble"] = ("treble", "tiz", "aigus", "Höhen"),
		["eqn-vocal"] = ("vocal", "vokal", "voix", "Gesang"),
		["eqn-pop"] = ("pop", "pop", "pop", "Pop"),
		["eqn-rock"] = ("rock", "rock", "rock", "Rock"),
		["eqn-classical"] = ("classical", "klasik", "classique", "Klassik"),
		["eqn-electronic"] = ("electronic", "elektronik", "électro", "Elektro"),
		["eqn-jazz"] = ("jazz", "caz", "jazz", "Jazz"),
		["eqn-custom"] = ("custom", "özel", "perso", "eigene"),
		["fx-now"] = ("✨ Sound now: {0}. [b]{1}fx nightcore[/b], [b]slowed[/b], [b]8d[/b] or [b]off[/b]; [b]{1}speed 1.25[/b].", "✨ Şu anki ses: {0}. [b]{1}fx nightcore[/b], [b]yavaş[/b], [b]8d[/b] ya da [b]kapat[/b]; [b]{1}hız 1.25[/b].", "✨ Son actuel : {0}. [b]{1}fx nightcore[/b], [b]ralenti[/b], [b]8d[/b] ou [b]non[/b] ; [b]{1}vitesse 1.25[/b].", "✨ Klang gerade: {0}. [b]{1}fx nightcore[/b], [b]langsam[/b], [b]8d[/b] oder [b]aus[/b]; [b]{1}tempo 1.25[/b]."),
		["fx-none"] = ("no effects", "efekt yok", "aucun effet", "keine Effekte"),
		["fx-nightcore"] = ("nightcore", "nightcore", "nightcore", "Nightcore"),
		["fx-slowed"] = ("slowed", "yavaşlatılmış", "ralenti", "verlangsamt"),
		["fx-on"] = ("✨ [b]{0}[/b] on.", "✨ [b]{0}[/b] açık.", "✨ [b]{0}[/b] activé.", "✨ [b]{0}[/b] an."),
		["fx-effect-off"] = ("✨ [b]{0}[/b] off.", "✨ [b]{0}[/b] kapalı.", "✨ [b]{0}[/b] désactivé.", "✨ [b]{0}[/b] aus."),
		["fx-8d-on"] = ("🎧 8D on: the sound circles around your head.", "🎧 8D açık: ses başının etrafında döner.", "🎧 8D activé : le son tourne autour de ta tête.", "🎧 8D an: der Klang kreist um deinen Kopf."),
		["fx-8d-off"] = ("🎧 8D off.", "🎧 8D kapalı.", "🎧 8D désactivé.", "🎧 8D aus."),
		["fx-off"] = ("✨ Effects off, normal speed.", "✨ Efektler kapalı, normal hız.", "✨ Effets désactivés, vitesse normale.", "✨ Effekte aus, normale Geschwindigkeit."),
		["fx-unknown"] = ("Unknown effect [b]{0}[/b]. Try [b]{1}fx nightcore[/b], [b]slowed[/b], [b]8d[/b] or [b]off[/b].", "[b]{0}[/b] diye bir efekt yok. [b]{1}fx nightcore[/b], [b]yavaş[/b], [b]8d[/b] ya da [b]kapat[/b] dene.", "Effet inconnu : [b]{0}[/b]. Essaie [b]{1}fx nightcore[/b], [b]ralenti[/b], [b]8d[/b] ou [b]non[/b].", "Unbekannter Effekt [b]{0}[/b]. Versuch [b]{1}fx nightcore[/b], [b]langsam[/b], [b]8d[/b] oder [b]aus[/b]."),
		["speed-now"] = ("⏩ Speed: [b]{0}[/b]. Change it with [b]{1}speed 0.5[/b] … [b]2[/b].", "⏩ Hız: [b]{0}[/b]. [b]{1}hız 0.5[/b] … [b]2[/b] ile değiştir.", "⏩ Vitesse : [b]{0}[/b]. Change-la avec [b]{1}vitesse 0.5[/b] … [b]2[/b].", "⏩ Tempo: [b]{0}[/b]. Ändern mit [b]{1}tempo 0.5[/b] … [b]2[/b]."),
		["speed-set"] = ("⏩ Speed [b]{0}[/b].", "⏩ Hız [b]{0}[/b].", "⏩ Vitesse [b]{0}[/b].", "⏩ Tempo [b]{0}[/b]."),
		["speed-bad"] = ("The speed goes from 0.5 to 2, like [b]{0}speed 1.25[/b].", "Hız 0.5 ile 2 arasında olur, örneğin [b]{0}hız 1.25[/b].", "La vitesse va de 0.5 à 2, par exemple [b]{0}vitesse 1.25[/b].", "Das Tempo geht von 0.5 bis 2, zum Beispiel [b]{0}tempo 1.25[/b]."),
		["list-missed"] = ("{0} weren't found.", "{0} şarkı bulunamadı.", "{0} n'ont pas été trouvés.", "{0} wurden nicht gefunden."),
		["playlist-added"] = ("📃 Added {0} songs from [b]{1}[/b].", "📃 [b]{1}[/b] listesinden {0} şarkı sıraya eklendi.", "📃 {0} morceaux de [b]{1}[/b] ajoutés à la file.", "📃 {0} Songs aus [b]{1}[/b] hinzugefügt."),
		["playlist-not-found"] = ("There is no playlist called [b]{0}[/b]. {1}", "[b]{0}[/b] adında bir liste yok. {1}", "Il n'y a pas de playlist nommée [b]{0}[/b]. {1}", "Es gibt keine Playlist namens [b]{0}[/b]. {1}"),
		["playlists"] = ("📃 Playlists: {0}", "📃 Listeler: {0}", "📃 Playlists : {0}", "📃 Playlists: {0}"),
		["playlists-none"] = ("No saved playlists yet. Make one in the panel.", "Henüz kayıtlı liste yok. Panelden oluşturabilirsin.", "Aucune playlist enregistrée pour l'instant. Crée-en une dans le panneau.", "Noch keine gespeicherten Playlists. Erstelle eine im Panel."),
		["favorites"] = ("Favorites", "Favoriler", "Favoris", "Favoriten"),
		["playlist-full"] = ("[b]{0}[/b] is full ({1} songs). Remove some in the panel first.", "[b]{0}[/b] dolu ({1} şarkı). Önce panelden birkaçını sil.", "[b]{0}[/b] est pleine ({1} morceaux). Retire d'abord quelques morceaux dans le panneau.", "[b]{0}[/b] ist voll ({1} Songs). Entferne zuerst ein paar im Panel."),
		["fav-added"] = ("⭐ Saved to [b]{1}[/b]: {0}", "⭐ [b]{1}[/b] listesine eklendi: {0}", "⭐ Ajouté à [b]{1}[/b] : {0}", "⭐ In [b]{1}[/b] gespeichert: {0}"),
		["help"] = (
			"🐼 [b]Panda[/b] commands:\n" +
			"[b]{0}play[/b] <song or link> – play / add to queue (YouTube, SoundCloud, Spotify, radio links…; [b]sc:[/b] / [b]ytm:[/b] searches SoundCloud / YouTube Music) · [b]{0}playnow[/b] <song> – play right away\n" +
			"[b]{0}skip[/b] – next song · [b]{0}stop[/b] – stop & clear\n" +
			"[b]{0}pause[/b] · [b]{0}resume[/b]\n" +
			"[b]{0}queue[/b] – show queue · [b]{0}np[/b] – now playing\n" +
			"[b]{0}vol[/b] <0-100> · [b]{0}loop[/b] <off|one|all> · [b]{0}shuffle[/b]\n" +
			"[b]{0}remove[/b] <#> · [b]{0}clear[/b] · [b]{0}join[/b] – come to my channel · [b]{0}follow[/b] – follow me around\n" +
			"[b]{0}search[/b] <song> – pick from the results · [b]{0}seek[/b] 1:30 · [b]{0}back[/b] – previous song · [b]{0}skipto[/b] <#> · [b]{0}move[/b] <#> <#> · [b]{0}voteskip[/b]\n" +
			"[b]{0}autoplay[/b] <on|off> – keep playing similar songs when the queue ends · [b]{0}fm[/b] <station> – internet radio (alone: the favorite stations)\n" +
			"[b]{0}eq[/b] <bass|vocal|rock…|off> – equalizer · [b]{0}fx[/b] <nightcore|slowed|8d|off> – effects · [b]{0}speed[/b] <0.5–2>\n" +
			"[b]{0}playlist[/b] <name> – queue a saved playlist · [b]{0}playlists[/b] · [b]{0}fav[/b] – save this song to Favorites · [b]{0}myfav[/b] – your own list (play, list, remove)",
			"🐼 [b]Panda[/b] komutları:\n" +
			"[b]{0}çal[/b] <şarkı veya link> – çal / sıraya ekle (YouTube, SoundCloud, Spotify, radyo linkleri…; [b]sc:[/b] / [b]ytm:[/b] SoundCloud / YouTube Music'te arar) · [b]{0}şimdiçal[/b] <şarkı> – hemen çal\n" +
			"[b]{0}geç[/b] – sonraki şarkı · [b]{0}dur[/b] – durdur ve sırayı temizle\n" +
			"[b]{0}duraklat[/b] · [b]{0}devam[/b]\n" +
			"[b]{0}sıra[/b] – sırayı göster · [b]{0}şimdi[/b] – çalan şarkı\n" +
			"[b]{0}ses[/b] <0-100> · [b]{0}tekrar[/b] <kapalı|şarkı|hepsi> · [b]{0}karıştır[/b]\n" +
			"[b]{0}sil[/b] <#> · [b]{0}temizle[/b] · [b]{0}gel[/b] – kanalıma gel · [b]{0}takip[/b] – beni takip et\n" +
			"[b]{0}ara[/b] <şarkı> – sonuçlardan seç · [b]{0}sar[/b] 1:30 · [b]{0}önceki[/b] – önceki şarkı · [b]{0}git[/b] <#> · [b]{0}taşı[/b] <#> <#> · [b]{0}oyla[/b] – geçmek için oy\n" +
			"[b]{0}otomatik[/b] <aç|kapat> – sıra bitince benzer şarkılar çalmaya devam et · [b]{0}fm[/b] <radyo> – internet radyosu (tek başına: favori radyolar)\n" +
			"[b]{0}eq[/b] <bas|vokal|rock…|kapat> – ekolayzer · [b]{0}efekt[/b] <nightcore|yavaş|8d|kapat> – efektler · [b]{0}hız[/b] <0.5–2>\n" +
			"[b]{0}liste[/b] <ad> – kayıtlı listeyi sıraya ekle · [b]{0}listeler[/b] · [b]{0}fav[/b] – bu şarkıyı Favoriler'e kaydet · [b]{0}myfav[/b] – kendi listen (çal, liste, sil)\n" +
			"(English commands like [b]{0}play[/b] work too)",
			"🐼 Commandes de [b]Panda[/b] :\n" +
			"[b]{0}jouer[/b] <morceau ou lien> – lire / ajouter à la file (YouTube, SoundCloud, Spotify, liens radio… ; [b]sc:[/b] / [b]ytm:[/b] cherche sur SoundCloud / YouTube Music) · [b]{0}jouermaintenant[/b] <morceau> – lire tout de suite\n" +
			"[b]{0}passer[/b] – morceau suivant · [b]{0}arrêt[/b] – arrêter et vider la file\n" +
			"[b]{0}pause[/b] · [b]{0}reprendre[/b]\n" +
			"[b]{0}file[/b] – voir la file · [b]{0}encours[/b] – morceau en cours\n" +
			"[b]{0}volume[/b] <0-100> · [b]{0}boucle[/b] <non|un|tout> · [b]{0}mélanger[/b]\n" +
			"[b]{0}retirer[/b] <#> · [b]{0}vider[/b] · [b]{0}viens[/b] – viens dans mon canal · [b]{0}suivre[/b] – suis-moi de canal en canal\n" +
			"[b]{0}chercher[/b] <morceau> – choisir parmi les résultats · [b]{0}aller[/b] 1:30 · [b]{0}précédent[/b] · [b]{0}allerà[/b] <#> · [b]{0}déplacer[/b] <#> <#> · [b]{0}voter[/b] – voter pour passer\n" +
			"[b]{0}auto[/b] <oui|non> – continuer avec des morceaux proches quand la file est finie · [b]{0}fm[/b] <station> – radio sur Internet (seul : les radios favorites)\n" +
			"[b]{0}eq[/b] <basses|voix|rock…|non> – égaliseur · [b]{0}effet[/b] <nightcore|ralenti|8d|non> – effets · [b]{0}vitesse[/b] <0.5–2>\n" +
			"[b]{0}playlist[/b] <nom> – ajouter une playlist enregistrée · [b]{0}playlists[/b] · [b]{0}fav[/b] – enregistrer ce morceau dans les Favoris · [b]{0}myfav[/b] – ta propre liste (jouer, liste, retirer)\n" +
			"(les commandes anglaises comme [b]{0}play[/b] marchent aussi)",
			"🐼 [b]Panda[/b]-Befehle:\n" +
			"[b]{0}spielen[/b] <Song oder Link> – abspielen / einreihen (YouTube, SoundCloud, Spotify, Radio-Links…; [b]sc:[/b] / [b]ytm:[/b] sucht bei SoundCloud / YouTube Music) · [b]{0}jetztspielen[/b] <Song> – sofort abspielen\n" +
			"[b]{0}überspringen[/b] – nächster Song · [b]{0}stopp[/b] – stoppen und Warteschlange leeren\n" +
			"[b]{0}pause[/b] · [b]{0}fortsetzen[/b]\n" +
			"[b]{0}warteschlange[/b] – Warteschlange zeigen · [b]{0}läuft[/b] – was gerade läuft\n" +
			"[b]{0}lautstärke[/b] <0-100> · [b]{0}wiederholen[/b] <aus|eins|alle> · [b]{0}mischen[/b]\n" +
			"[b]{0}entfernen[/b] <#> · [b]{0}leeren[/b] · [b]{0}komm[/b] – komm in meinen Channel · [b]{0}folgen[/b] – folge mir durch die Channels\n" +
			"[b]{0}suchen[/b] <Song> – aus den Ergebnissen wählen · [b]{0}spulen[/b] 1:30 · [b]{0}zurück[/b] · [b]{0}springen[/b] <#> · [b]{0}verschieben[/b] <#> <#> · [b]{0}abstimmen[/b] – für das Überspringen abstimmen\n" +
			"[b]{0}autoplay[/b] <an|aus> – ähnliche Songs weiterspielen, wenn die Warteschlange leer ist · [b]{0}fm[/b] <Sender> – Internetradio (allein: die Lieblingssender)\n" +
			"[b]{0}eq[/b] <bass|gesang|rock…|aus> – Equalizer · [b]{0}effekt[/b] <nightcore|langsam|8d|aus> – Effekte · [b]{0}tempo[/b] <0.5–2>\n" +
			"[b]{0}playlist[/b] <Name> – gespeicherte Playlist einreihen · [b]{0}playlists[/b] · [b]{0}fav[/b] – diesen Song in den Favoriten speichern · [b]{0}myfav[/b] – deine eigene Liste (spielen, liste, entfernen)\n" +
			"(englische Befehle wie [b]{0}play[/b] gehen auch)"),
	};

	/// <summary>Chat languages, in the order the panel offers them.</summary>
	public static readonly string[] Languages = ["en", "tr", "fr", "de"];

	public static string Get(string lang, string key, params object[] args)
	{
		if (!All.TryGetValue(key, out var s))
			return key;
		var text = lang switch { "tr" => s.Tr, "fr" => s.Fr, "de" => s.De, _ => s.En };
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
		"source-off" or "link-private" or "link-unreachable" or "link-unplayable" => Get(lang, "err-" + reason),
		// Text of yt-dlp or ffmpeg, maybe about someone's page: no BBCode.
		_ => Get(lang, "err-other", reason.Replace("[", "(").Replace("]", ")")),
	};

	public static bool IsKnownError(string reason) =>
		reason is "not-found" or "empty" or "playlist-empty" or "not-youtube" or "too-long" or "bot-check" or "unavailable" or "age" or "timeout"
			or "source-off" or "link-private" or "link-unreachable" or "link-unplayable";

	/// <summary>The reason without the "Couldn't play that:" wrapper, for "Couldn't play X: reason".</summary>
	public static string ShortError(string lang, string reason) =>
		IsKnownError(reason) ? Error(lang, reason) : reason.Replace("[", "(").Replace("]", ")");
}
