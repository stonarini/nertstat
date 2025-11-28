# nertstat

## Docs

### Primary Endpoint: `/`

The primary endpoint can serve data in multiple formats based on the HTTP `Accept` header.

- **Default (HTML):** `Accept: text/html` or no header. Returns a minimal HTML interface.
- **JSON:** `Accept: application/json`. Returns a JSON array of lobby objects.
- **XML:** `Accept: application/xml`. Returns an XML representation of the lobbies.
- **Plain Text:** `Accept: text/plain`. Returns a simple text-based list of lobbies.

#### Example Usage with cURL:

```shell
# For JSON
curl -H "Accept: application/json" https://nertstat.dost.pt/

# For XML
curl -H "Accept: application/xml" https://nertstat.dost.pt/

# For Plain Text
curl -H "Accept: text/plain" https://nertstat.dost.pt/
```

### Other Endpoints

- `/classic`: This is a "replica" of the NERTS! Online main page.
- `/{timestamp}.gif`: A dynamically generated image of the current lobby status. Really it works with any *"timestamp"*, since it's just a trick to invalidate caches.

---

## /rant
This project was inspired by [vpzom](https://github.com/vpzomtrrfrt), specifically its [Is it Nerts time?](https://isitnertstime.vpzom.click/) page.  

I'm a big Zachtronics fan, and I was introduced to them by NERTS! Online, a game that holds a special place in my library.  
After all, it's *competitive solitaire*.

Recently, I started playing again and used vpzom's page to check for active games without having to launch the game itself.
This sparked my curiosity. Since the game has no public API, I wanted to know *how* he did it.

So, I started to investigate. At first, I thought he had reverse-engineered it, so I downloaded Wireshark and sniffed the game's traffic. What I found was that the game wasn't querying any private service, but was only making calls to Steam. 
This led me to discover the Steamworks SDK, which is used quite a bit to add multiplayer capabilities to games. 
Now I needed a way to query this data so I kept digging. That's when I found SteamKit2, a C# library that does exactly what I needed.

Now, I'm not a fan of C# or .NET in general, but since SteamKit2 seemed to be the best option, and every other similar library is *based* on it, I resigned myself to using it for this project.

With everything ready, I started fiddling to make it work. I wanted this to be a light and quick project, but it ended up being more complex than I expected. The SteamKit2 library doesn't have great documentation, and I also struggled to find a way to represent the data graphically.

In the end, I created a "replica" of the game's main page UI with the help of images and CSS tricks.
Overall, I thought it was okay, if a little unimaginative.

Incidentally, I had ordered the *Zach-Like* documentary book, and on the very first page, I found this quote:
> "Explore your own ideas and interests and use them to create your own games, just like we have here."

For anyone who has read the book, I know the quote doesn't really apply to this project, but it stuck with me anyway. 
It felt kinda wrong to settle for just a replica. So, I pushed aside the original "UI" and went for something a bit more interesting.
In the end, it's still a kind of 'copy' of vpzom's page, but at least I can point to a few differences.

This isn't really meant for public use, but more as a reference for making your own (or something similar).
The project is hosted at [nertstat.dost.pt](https://nertstat.dost.pt/), and you can query it. There are no restrictions for now, though I might add rate-limiting if I see heavy usage.

---

## Disclaimer

This project is a non-commercial, fan-made tribute and is not affiliated with, endorsed, or sponsored by Zachtronics. 
All trademarks, game titles, and assets, including the NERTS! Online logo and background screenshots, are the property of their respective owners.

NERTS! Online is (c) 2021 Zachtronics. All rights reserved.

## License

The code in this project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Acknowledgements

This project utilizes several open-source libraries. For a full list of dependencies and their respective licenses, please see the [NOTICE.md](NOTICE.md) file.

