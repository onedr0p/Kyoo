// Kyoo - A portable and vast media library solution.
// Copyright (c) Kyoo.
//
// See AUTHORS.md and LICENSE file in the project root for full license information.
//
// Kyoo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// any later version.
//
// Kyoo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Kyoo. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.Threading.Tasks;
using Kyoo.Abstractions.Controllers;
using Kyoo.Abstractions.Models;
using Kyoo.Abstractions.Models.Permissions;
using Microsoft.AspNetCore.Mvc;

namespace Kyoo.Core.Api
{
	[Route("api/search/{query}")]
	[ApiController]
	public class SearchApi : ControllerBase
	{
		private readonly ILibraryManager _libraryManager;

		public SearchApi(ILibraryManager libraryManager)
		{
			_libraryManager = libraryManager;
		}

		[HttpGet]
		[Permission(nameof(Collection), Kind.Read)]
		[Permission(nameof(Show), Kind.Read)]
		[Permission(nameof(Episode), Kind.Read)]
		[Permission(nameof(People), Kind.Read)]
		[Permission(nameof(Genre), Kind.Read)]
		[Permission(nameof(Studio), Kind.Read)]
		public async Task<ActionResult<SearchResult>> Search(string query)
		{
			return new SearchResult
			{
				Query = query,
				Collections = await _libraryManager.Search<Collection>(query),
				Shows = await _libraryManager.Search<Show>(query),
				Episodes = await _libraryManager.Search<Episode>(query),
				People = await _libraryManager.Search<People>(query),
				Genres = await _libraryManager.Search<Genre>(query),
				Studios = await _libraryManager.Search<Studio>(query)
			};
		}

		[HttpGet("collection")]
		[HttpGet("collections")]
		[Permission(nameof(Collection), Kind.Read)]
		public Task<ICollection<Collection>> SearchCollections(string query)
		{
			return _libraryManager.Search<Collection>(query);
		}

		[HttpGet("show")]
		[HttpGet("shows")]
		[Permission(nameof(Show), Kind.Read)]
		public Task<ICollection<Show>> SearchShows(string query)
		{
			return _libraryManager.Search<Show>(query);
		}

		[HttpGet("episode")]
		[HttpGet("episodes")]
		[Permission(nameof(Episode), Kind.Read)]
		public Task<ICollection<Episode>> SearchEpisodes(string query)
		{
			return _libraryManager.Search<Episode>(query);
		}

		[HttpGet("people")]
		[Permission(nameof(People), Kind.Read)]
		public Task<ICollection<People>> SearchPeople(string query)
		{
			return _libraryManager.Search<People>(query);
		}

		[HttpGet("genre")]
		[HttpGet("genres")]
		[Permission(nameof(Genre), Kind.Read)]
		public Task<ICollection<Genre>> SearchGenres(string query)
		{
			return _libraryManager.Search<Genre>(query);
		}

		[HttpGet("studio")]
		[HttpGet("studios")]
		[Permission(nameof(Studio), Kind.Read)]
		public Task<ICollection<Studio>> SearchStudios(string query)
		{
			return _libraryManager.Search<Studio>(query);
		}
	}
}