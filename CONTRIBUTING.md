# Contributing

Contributions are appreciated. As a mostly single developer of this project there are too many things that I'd like to do. But I don't have time to implement/fix everything and burnout is a thing. There is a lot of code, some good some bad. Newer code is generally of a better quality.

### For smaller Issues and Features

Generally for smaller stuff it is fine to create a PR with the changes.

### For lager Issues and Features

Its preferable to create either an Issue, discussion item or draft PR. This is because it is usually better to discuss these before creating a large PR with many changes. Is is also easier for me as a the code owner to be able to groom larger changes.

### Pull Request Process

This repo always squash and merge commits. Therefore, intermediary commits aren't that important. [Release please](https://github.com/googleapis/release-please-action) is used to automate releases and changelog. Also see [Conventional commits](https://www.conventionalcommits.org/en/v1.0.0/)

1. Do test things locally first
2. Create PR, add relevant description.
3. Make sure github tests succeed (some may have to be manually run by code owners)
4. Code owners will do a code review.
5. Depending on whether the commits use convention commits format, the code owner may modify the commit header and body when merging the PR. Alternatively you can write the changelog entries in the PR description or title.
