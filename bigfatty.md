This application has been vibecoded and needs a thorough review/research to push it over the line.

The repository provides integration with the Hevy APP and aims to translatethe attatch A2S 2024-2025 (2).xslx spreadsheet to a web application.

In the spreadsheet you will find two progression schemes, LinearProgression and RepsPerSet, these are the current two implementations.

I have my doubt the application currently integrates the same way as the spreadsheet, I do not ahve confidence in any of the tests that are integration/e2e level.

I want to remove all E2E tests and ensure all our integration tests pass, we need to have end to end coverage of a 21 week workout, look at A2S2_Validation_data.md this is the expected output for the two linear progression(also known as hypertrophy exercises) 

We need all cases covered for where a set/exercise can progress for the next week, f/e hyper trophy needs -2, -1 0, 1,2,3,4,5 reps over the target total f/e

Reps Per set should increase as expected.

We also have unilateral functionallity, currently this can be done at any point, In reality when selecting an exercise during the workout creation screen we should set it Unilateral there, when unilateral we need to send 2 sets instead of 1, so if we have a target of 3 sets and 8 reps its really 6 sets and 8 reps as it is done unilaterally.

Average 2 Savage has the six week blocks/mesocycles this needs to be built in the the workotu creation flow, once a block is compelted you can either restart it, restarting it will use the existing training maxes/reps per set progression just using week 1s formula so the first exercise of a new cycle with have the final training max of the prior cycle.

Please review all UI/UX appraoches on this app and tidy up where appropriate.

Follow best architectural practices and review the DDD implementation and make changes where necessary.

Ensure all training data such as reps per sets is persisted so we can review all data needed for our application, our application just focuses on the progression so we just need the exercise identifiers and reps per set etc and dates to log against so we can build out features such as graphs for training max evaluation. 

Please analyse the spreadsheet and validation dat anad ensure our tests are critically correct, remove any junk/debunk tests such as playwright in the front end and the backend, our testing is just unit tests, ui tests (for front end components) and integration tests using WebApplication FActory.

To pass I expect a full 21 week cycle with linear progression primary and auxilarry lifts, at least one unilateral exercise and range of reps per set exercises. Hyper trophy cannot be unilateral, only reps per set can. I expecft atleast another test for ensuring wqe start a new block correctly.

Exercise substition, for hypertrophy it should just substitute for that week and the training max stays the same, then the next week we proceed with the next weeks formula just using the same training max.

Reps per set is the same behaviour, just skip progression for this week and dont do anything and pick it back up next week.