---
apply: always
---

The workflow for a new feature/fix is:
- generate a working name for the feature, from here called <feature_name>
- check out a new feature/<feature_name> or fix/<feature_name> branch
- create a planning document under <project_root>/plans/<feature_name>.md
- go through the four dev roles and do commits as needed/sane
- the build must run clean now and the user must test the changes and confirm a clean run
- do a pull request on github