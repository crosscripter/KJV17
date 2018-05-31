using System;
using System.IO;
using System.Text;
using System.Linq;
using Elise.Sources;
using static System.Console;
using System.Collections.Generic;
using System.Text.RegularExpressions;

class KJV17
{
	static string AppendVerse(string template, string tag, Dictionary<string,object> parameters)
	{
		var start = template.IndexOf($"<{tag}>", 0);
		var endTag = $"</{tag}>";
		var stop = template.IndexOf(endTag, start) + endTag.Length;
		var indent = "					";
		var stamp = indent + template.Substring(start, stop - start);

		foreach (var param in parameters) 
			stamp = stamp.Replace("{" + param.Key + "}", param.Value.ToString());

		var last = template.LastIndexOf(endTag) + endTag.Length;		
		if (last == stop) last += 3;
		return template.Insert(last, "\n" + stamp);
	}

	static string StripReference(string text)
	{
		var start = text.IndexOf(":", 0);
		var stop = text.IndexOf(" ", start);
		return text.Substring(stop).Trim();
	}

	static readonly Dictionary<string,string> BookInfo = new Dictionary<string,string>
	{
		{"GE", "Genesis|The First Book of Moses Called"},
		{"RO", "Romans|The Epistle of Paul to the"},
		{"JOH", "John|The Gospel According to"}
	};
	
	static readonly Dictionary<string,string> Updates = new Dictionary<string,string>
	{
		// Pronouns
		{"thou", "you"},
		{"thee", "you"},
		{"thy", "your"},
		{"mine", "my"},
		{"ye", "you all"}, //<sub>pl</sub>"},
		{"thine", "yours"},
		{"thyself", "yourself"},
		{"hath", "has"},
		{"hast", "have"},
		{"art", "are"},
		{"wast", "was"},
		{"shalt", "shall"},
		{"doth", "does"},
		{"lest", "for fear that"},
		
		// Spellings
		{"yea", "yes"},
		{"nay", "no"},
		{"unto", "to"},
		{"saith", "says"},
		{"whence", "where"},
		{"hence", "here"},
		{"hither", "here"},
		{"thither", "there"},
		{"whosoever", "whoever"},
		{"whatsoever", "whatever"},
		{"bridegroom", "groom"},
		{"tarry", "wait"},
		{"pray", "ask"},
		{"verily, verily", "very truly"},
		// {"verily", "truly"},
		{"hearken", "listen"},
		{"subtil", "subtle"}
	};

	static string ItalicizeAdded(string text) 
	{
		return Regex.Replace(text, @"\[([\w ]+)\]", "<em>$1</em>");
	}

	static bool HasDiety(string text)
	{
		return Regex.IsMatch(text, @"(God|Jesus|Lord|(Holy)? Spirit)");
	}

	static string AddQuotes(string text)
	{		
		var qclass = HasDiety(text) ? "red" : string.Empty;
		var qtitle = HasDiety(text) ? "Words of God" : "Dialogue";

		return Regex.Replace(
			text, 
			@", ([A-Z][^:\.]+)([:\.])", 
			$", <blockquote title='{qtitle}'><q class='{qclass}'>$1$2</q></blockquote>"
		);
	}

	static string UpdateGrammar(string text)
	{
		foreach (var update in Updates)
		{
			text = Regex.Replace(
				text, $@"\b{update.Key}\b",
				$"<strong title='{update.Key}'>{update.Value}</strong>",
				RegexOptions.IgnoreCase
			);
		}

		text = Regex.Replace(
			text, @"\b([\w]{2,})est\b", 
			"<strong title='$1est'>$1e</strong>"
		);

		text = Regex.Replace(
			text, @"\byou\s*([\w]{2,})st\b", 
			"<strong title='$1st'>you $1</strong>"
		);

		text = Regex.Replace(
			text, @"\b([\w]+)eth\b", 
			"<strong title='$1eth'>$1es</strong>"
		);

		text = Regex.Replace(
			text, @"\b([\w]+) not\b",
			"<strong title='archaic: $1 not'>$1 not</strong>"
		);

		return text;
	}

	static string CapitalizeDiety(string text)
	{
		if (HasDiety(text))
		{
			text = Regex.Replace(
				text, @"\b(he|him|his|himself)\b", 
				"<span title='Reference to Diety' class='diety'>$1</span>",
				RegexOptions.IgnoreCase
			);

			text = text.Replace("diety'>h", "diety'>H");
		}

		return text;
	}

	static string[] Outline = File.ReadAllLines("outline.txt");

	static string AppendHeading(string template, string book, int chapter, int verse)
	{
		var chapterRef = $"{book} {chapter}:";
		var inChapter = false;
		var heading = string.Empty;

		foreach (var line in Outline)
		{
			if (line.StartsWith(chapterRef))
			{
				inChapter = true;
				continue;
			}

			if (inChapter && line.StartsWith($"{verse}. "))
				heading = line.Split('.')[1].Replace(";", string.Empty).Trim();
			else if (inChapter && !line.Contains(". "))
				break;
		}

		if (!string.IsNullOrEmpty(heading))
		{
			var words = heading.Split(' ');
			heading = string.Join(" ", words.Select(w => char.ToUpper(w[0]) + w.Substring(1)));			
			var start = template.IndexOf($"<span><sup>{verse}</sup>");
			template = template.Insert(start, $"\n					<h4>{heading}</h4>\n					");
		}

		return template;
	}

	static void Main()
	{
		var KJV = new Bible();
		var template = File.ReadAllText("index.html");

		foreach (var book in KJV.Books)
		{
			var name = book;
			var intro = string.Empty;

			if (BookInfo.ContainsKey(book.ToUpper()))
			{
				var info = BookInfo[book.ToUpper()].Split('|');
				name = info[0];
				intro = info.Length > 1 ? info[1] : string.Empty;
			}

			template = template.Replace("{book}", name)
							   .Replace("{intro}", intro);

			Directory.CreateDirectory(name);

			for (var chapter = 1; chapter <= KJV.Chapters(book); chapter++)
			{
				var html = template.Replace("{chapter}", chapter.ToString());

				for (var verse = 1; verse <= KJV.Verses(book, chapter); verse++) 
				{					
					var text = KJV.Record(book, chapter, verse);
					text = StripReference(text);
					text = AddQuotes(text);
					text = ItalicizeAdded(text);
					text = UpdateGrammar(text);
					text = CapitalizeDiety(text);

					html = AppendVerse(html, "span", new Dictionary<string,object>
					{
						{"verse", verse}, 
						{"text", text}
					});

					html = AppendHeading(html, name, chapter, verse);
				}

				File.WriteAllText($@"{name}/{chapter:D2}.html", html);
			}
		}
	}
}
