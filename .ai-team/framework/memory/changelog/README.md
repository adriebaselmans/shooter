# Changelog
#
# Append-only audit trail of wiki writes.
# One file per month: <YYYY-MM>.yaml
# Entries are appended by the wiki-write skill after every page create/update.
#
# Format per entry:
#   - ts: <ISO-8601 UTC>
#     role: <role that wrote>
#     action: <created|updated|archived>
#     target: wiki/<category>/<page-id>
#     summary: "<what changed, max 80 chars>"
