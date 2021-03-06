﻿using System.Text.RegularExpressions;

using Raven.Client;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Session;
using Raven.Yabt.Database.Common.References;
using Raven.Yabt.Database.Models.BacklogItems;
using Raven.Yabt.Database.Models.BacklogItems.Indexes;
using Raven.Yabt.Database.Models.Users;
using Raven.Yabt.Domain.Helpers;
using Raven.Yabt.Domain.Infrastructure;
using Raven.Yabt.Domain.UserServices.Command;

namespace Raven.Yabt.Domain.BacklogItemServices.Commands
{
	internal class UpdateUserReferencesCommand : IUpdateUserReferencesCommand
	{
		private readonly IPatchOperationsAddDeferred _patchOperations;
		private readonly IAsyncDocumentSession _dbSession;

		public UpdateUserReferencesCommand(IAsyncDocumentSession dbSession, IPatchOperationsAddDeferred patchOperations)
		{
			_dbSession = dbSession;
			_patchOperations = patchOperations;
		}

		public void ClearUserId(string userId)
		{
			// Replace invalid characters with empty strings. Can't pass it as a parameter, as string parameters get wrapped in '\"' when inserted
			var sanitisedUserId = Regex.Replace(userId, @"[^\w\.@-]", "");
			// Get full ID
			var idForDynamicField = _dbSession.GetIdForDynamicField<User>(sanitisedUserId);
			var fullId = _dbSession.GetFullId<User>(userId);

			// Form a patch query
			var queryString = $@"FROM INDEX '{new BacklogItems_ForList().IndexName}' AS i
								WHERE i.{nameof(BacklogItemIndexedForList.ModifiedBy)}_{idForDynamicField} != null OR i.{nameof(BacklogItemIndexedForList.AssignedUserId)} == $userId
								UPDATE
								{{
									if (i.{nameof(BacklogItemIndexedForList.Assignee)}.{nameof(UserReference.Id)}.toLowerCase() == $userId) {{
										i.{nameof(BacklogItemIndexedForList.Assignee)} = null;
									}}
									i.{nameof(BacklogItem.Comments)}.forEach(comment => {{
										if (comment.{nameof(Comment.Author)}.{nameof(UserReference.Id)}.toLowerCase() == $userId) {{
											comment.{nameof(Comment.Author)}.{nameof(UserReference.Id)} = null;
										}}
									}});
									i.{nameof(BacklogItem.ModifiedBy)}.forEach(modif => {{
																			if (modif.{nameof(BacklogItemHistoryRecord.ActionedBy)}.{nameof(UserReference.Id)}.toLowerCase() == $userId)
																				modif.{nameof(BacklogItemHistoryRecord.ActionedBy)}.{nameof(UserReference.Id)} = null;
																		}});
								}}";
			var query = new IndexQuery
			{
				Query = queryString,
				QueryParameters = new Parameters
				{
					{ "userId", fullId.ToLower() }
				}
			};

			// Add the patch to a collection
			_patchOperations.AddDeferredPatchQuery(query);
		}

		public void UpdateReferences(UserReference newUserReference)
		{
			if (string.IsNullOrEmpty(newUserReference.Id))
				return;
			
			// Replace invalid characters with empty strings. Can't pass it as a parameter, as string parameters get wrapped in '\"' when inserted
			var idForDynamicField = Regex.Replace(newUserReference.Id, @"[^\w\.@-]", "");

			// Form a patch query
			var queryString = $@"FROM INDEX '{new BacklogItems_ForList().IndexName}' AS i
								WHERE i.{nameof(BacklogItemIndexedForList.ModifiedBy)}_{idForDynamicField} != null OR i.{nameof(BacklogItemIndexedForList.AssignedUserId)} == $userId
								UPDATE
								{{
									if (i.{nameof(BacklogItemIndexedForList.Assignee)}.{nameof(UserReference.Id)}.toLowerCase() == $userId) {{
										i.{nameof(BacklogItemIndexedForList.Assignee)} = $userRef;
									}}
									i.{nameof(BacklogItem.Comments)}.forEach(comment => {{
										if (comment.{nameof(Comment.Author)}.{nameof(UserReference.Id)}.toLowerCase() == $userId) {{
											comment.{nameof(Comment.Author)} = $userRef;
										}}
									}});
									i.{nameof(BacklogItem.ModifiedBy)}.forEach(modif => {{
																			if (modif.{nameof(BacklogItemHistoryRecord.ActionedBy)}.{nameof(UserReference.Id)}.toLowerCase() == $userId)
																				modif.{nameof(BacklogItemHistoryRecord.ActionedBy)} = $userRef;
																		}});
								}}";
			var query = new IndexQuery
			{
				Query = queryString,
				QueryParameters = new Parameters
				{
					{ "userId", newUserReference.Id.ToLower() },
					{ "userRef", newUserReference },
				}
			};

			// Add the patch to a collection
			_patchOperations.AddDeferredPatchQuery(query);
		}
	}
}
