# Merged branch cleanup

(c) xpagedeveloper.com 2026

The `Delete Merged Branches` workflow removes repository branches that have already been merged into `main`.

## When it runs

The workflow runs:

- automatically when a pull request into `main` is closed as merged
- manually through `workflow_dispatch`
- weekly as a safety sweep

## Merge methods

Cleanup supports both:

- branches whose commits are ancestors of `main` (normal merge/rebase histories)
- branches whose pull request was merged into `main`, including squash merges

This is important because a squash-merged source branch is normally not a Git ancestor of the resulting squash commit on `main`.

## Safety rules

The workflow never deletes:

- `main`
- protected branches
- branches with an open pull request into `main`
- branches that have neither been merged by Git history nor by a merged pull request into `main`

The workflow requires `contents: write` to delete refs and `pull-requests: read` to verify merged/open pull requests.
