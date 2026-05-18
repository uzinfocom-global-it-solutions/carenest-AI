SELECT f."Id", f."Name", fm."UserId", fm."Role", fm."Status", u."Email"
FROM "Families" f
LEFT JOIN "FamilyMembers" fm ON fm."FamilyId" = f."Id"
LEFT JOIN "AspNetUsers" u ON u."Id" = fm."UserId"
ORDER BY f."Id";
